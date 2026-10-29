using System;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionWrackPrerequisite
{
    [Fact]
    public void WrackRequiresLearnedSiphonLifeNotJustThirtyOneTreePoints()
    {
        var build=ForeverWarlockTalentBuild.Default("affliction");
        build.Validate(60,"affliction");
        build.Ranks["siphon-life"]=0;
        Assert.Throws<ArgumentException>(()=>build.Validate(60,"affliction"));
        build.Ranks["siphon-life"]=1;
        build.Validate(60,"affliction");
    }
}
