using Okey101.Core.Model;
using Okey101.Core.V4;
namespace Okey101.Application
{
    public sealed class ScenarioLoader
    {
        private readonly ReplayCodec codec;
        public ScenarioLoader(string root) { codec = new ReplayCodec(root); }
        /// <summary>Stores a scenario: a 101-v1 record without commands (old engine) or a 101-v4 record (v4 engine). Other versions fail.</summary>
        public RuleResult<string> Import(string json)
        {
            if(json==null || json.Length>ReplayCodec.MaxBytes) return RuleResult<string>.Fail(ErrorCode.InvalidState);
            var parsed=ReplayCodec.DecodeAny(System.Text.Encoding.UTF8.GetBytes(json));
            if(!parsed.Success) return RuleResult<string>.Fail(ErrorCode.InvalidState);
            var name="scenario-"+System.Guid.NewGuid().ToString("N")+".json";
            if(parsed.Value.Engine==RecordEngine.Legacy)
            {
                if(parsed.Value.Legacy.Commands.Count!=0) return RuleResult<string>.Fail(ErrorCode.InvalidState);
                codec.Write(parsed.Value.Legacy,name);
            }
            else codec.WriteV4(parsed.Value.V4,name);
            return RuleResult<string>.Ok(name);
        }
        public RuleResult<RoundState> Load(string path)
        {
            var result = codec.Read(path);
            return result.Success && result.Value.Commands.Count == 0
                ? RuleResult<RoundState>.Ok(result.Value.InitialState) : RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
        }
        /// <summary>K-02: a 101-v4 scenario is the match state its record replays to (e.g. a rigged first hand, logic/scenarios R43/R44).</summary>
        public RuleResult<MatchState> LoadV4(string path)
        {
            var result = codec.ReadAny(path);
            if (!result.Success || result.Value.Engine != RecordEngine.V4) return RuleResult<MatchState>.Fail(ErrorCode.InvalidState);
            return V4Replay.Restore(result.Value.V4);
        }
        public RuleResult<bool> LoadInto(LocalSession session, string path)
        {
            var result = Load(path);
            if (!result.Success) return RuleResult<bool>.Fail(result.Error);
            session.Start(result.Value); return RuleResult<bool>.Ok(true);
        }
    }
}
