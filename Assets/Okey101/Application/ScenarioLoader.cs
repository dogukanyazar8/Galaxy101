using Okey101.Core.Model;
namespace Okey101.Application
{
    public sealed class ScenarioLoader
    {
        private readonly ReplayCodec codec;
        public ScenarioLoader(string root) { codec = new ReplayCodec(root); }
        public RuleResult<string> Import(string json)
        {
            if(json==null || json.Length>ReplayCodec.MaxBytes) return RuleResult<string>.Fail(ErrorCode.InvalidState);
            var parsed=ReplayCodec.Decode(System.Text.Encoding.UTF8.GetBytes(json),System.Threading.CancellationToken.None);
            if(!parsed.Success || parsed.Value.Commands.Count!=0) return RuleResult<string>.Fail(ErrorCode.InvalidState);
            var name="scenario-"+System.Guid.NewGuid().ToString("N")+".json";codec.Write(parsed.Value,name);return RuleResult<string>.Ok(name);
        }
        public RuleResult<RoundState> Load(string path)
        {
            var result = codec.Read(path);
            return result.Success && result.Value.Commands.Count == 0
                ? RuleResult<RoundState>.Ok(result.Value.InitialState) : RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
        }
        public RuleResult<bool> LoadInto(LocalSession session, string path)
        {
            var result = Load(path);
            if (!result.Success) return RuleResult<bool>.Fail(result.Error);
            session.Start(result.Value); return RuleResult<bool>.Ok(true);
        }
    }
}
