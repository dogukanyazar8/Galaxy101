using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Application
{
    public sealed class ReplayRecord
    {
        public const int SchemaVersion = 1;
        public const string RuleVersion = "101-v1", ShuffleVersion = "xorshift32-fy-v1";
        public uint Seed => InitialState.Seed;
        public RoundState InitialState { get; }
        public IReadOnlyList<GameCommand> Commands { get; }
        public string ExpectedFinalHash { get; }
        public ReplayRecord(RoundState initialState, IReadOnlyList<GameCommand> commands, string expectedFinalHash)
        { InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState)); Commands = Array.AsReadOnly(commands.ToArray()); ExpectedFinalHash = expectedFinalHash; }
    }
    public sealed class ReplayCodec
    {
        public const int MaxBytes = 20 * 1024 * 1024, MaxCommands = 2000;
        private readonly string root;
        public ReplayCodec(string root) { this.root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root))); }
        public void Write(ReplayRecord record, string path)
        {
            var target = Resolve(path);
            if (!Restore(record, CancellationToken.None).Success) throw new ArgumentException("Replay validation failed.", nameof(record));
            var bytes = DiagnosticsJson.Encode(DiagnosticsJson.Document(record));
            if (bytes.Length > MaxBytes) throw new ArgumentException("Replay exceeds the 20 MiB limit.", nameof(record));
            Directory.CreateDirectory(root); CheckAncestors(root);
            var temporary = Path.Combine(root, ".writing-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                // Move without overwrite publishes a complete file or leaves the old file intact.
                Resolve(path); File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public RuleResult<ReplayRecord> Read(string path) => Read(path, CancellationToken.None);
        public RuleResult<ReplayRecord> Read(string path, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested(); var target = Resolve(path);
                byte[] bytes;
                using (var stream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > MaxBytes) return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState);
                    using (var reader = new BinaryReader(stream)) bytes = reader.ReadBytes(MaxBytes + 1);
                }
                if (bytes.Length > MaxBytes) return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState);
                return Decode(bytes, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return RuleResult<ReplayRecord>.Fail(ErrorCode.Cancelled); }
            catch (IOException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch (UnauthorizedAccessException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch (ArgumentException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch (SerializationException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch (XmlException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch (OverflowException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch (FormatException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
        }
        internal static RuleResult<ReplayRecord> Decode(byte[] bytes, CancellationToken token)
        {
            if(bytes.Length>MaxBytes) return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState);
            try
            {
                var record=DiagnosticsJson.Record(DiagnosticsJson.Decode(bytes));var restored=Restore(record,token);
                return restored.Success?RuleResult<ReplayRecord>.Ok(record):RuleResult<ReplayRecord>.Fail(restored.Error);
            }
            catch(SerializationException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch(XmlException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch(ArgumentException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch(FormatException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
            catch(OverflowException) { return RuleResult<ReplayRecord>.Fail(ErrorCode.InvalidState); }
        }
        public static string Hash(RoundState state)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(DiagnosticsJson.Encode(DiagnosticsJson.Snapshot(state)))).Replace("-", "").ToLowerInvariant();
        }
        public static RuleResult<RoundState> Restore(ReplayRecord record, CancellationToken token)
        {
            if (record == null || record.Commands.Count > MaxCommands || !StateInvariant.ValidateAuthority(record.InitialState).Success)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var state = record.InitialState;
            foreach (var command in record.Commands)
            {
                if (token.IsCancellationRequested) return RuleResult<RoundState>.Fail(ErrorCode.Cancelled);
                var next = GameEngine.Apply(state, command, token);
                if (!next.Success) return RuleResult<RoundState>.Fail(next.Error);
                state = next.Value;
            }
            if (token.IsCancellationRequested) return RuleResult<RoundState>.Fail(ErrorCode.Cancelled);
            return string.Equals(Hash(state), record.ExpectedFinalHash, StringComparison.Ordinal)
                ? RuleResult<RoundState>.Ok(state) : RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
        }
        public string NewFileName() => "replay-" + Guid.NewGuid().ToString("N") + ".json";
        private string Resolve(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 100 || !name.EndsWith(".json", StringComparison.Ordinal)
                || name.Any(c => !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_' && c != '.')
                || name.Contains("..") || Path.GetFileName(name) != name || Path.IsPathRooted(name))
                throw new ArgumentException("Use a simple JSON filename inside Diagnostics.", nameof(name));
            var stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '0' && stem[3] <= '9'))
                throw new ArgumentException("Device filenames are not allowed.", nameof(name));
            CheckAncestors(root);
            var target = Path.Combine(root, name); CheckAncestors(target); return target;
        }
        private static void CheckAncestors(string path)
        {
            for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new ArgumentException("Diagnostics paths cannot traverse symbolic links or junctions.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
    }
}
