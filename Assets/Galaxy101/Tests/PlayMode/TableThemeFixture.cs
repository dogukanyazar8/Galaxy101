using Galaxy101.Table;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    /// <summary>Tables in tests wear Satürn whatever planet the editor's own menu state has saved.</summary>
    [SetUpFixture]
    public sealed class TableThemeFixture
    {
        [OneTimeSetUp] public void Pin() => TableScene.PlanetOverride = "Satürn";
        [OneTimeTearDown] public void Release() => TableScene.PlanetOverride = null;
    }
}
