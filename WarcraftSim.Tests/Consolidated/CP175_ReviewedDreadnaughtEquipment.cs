using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ReviewedDreadnaughtEquipment
{
    private static readonly (int Id,string Slot)[] Pieces=[
        (22416,"chest"),(22423,"wrist"),(22421,"hands"),(22422,"waist"),
        (22418,"head"),(22417,"legs"),(22419,"shoulder"),(22420,"feet")
    ];
    [Fact]
    public void EightRealReviewedPiecesAreSelectableAndApplyStaticStats()
    {
        var items=Pieces.Select(x=>ForeverEquipmentCatalog.FindItem(x.Id)).ToArray();
        Assert.Equal(8,items.Length);
        Assert.All(items,item=>{
            Assert.Equal("published-client-tooltip",item.VerificationStatus);
            Assert.Equal("reviewed-published-forever-tooltip",item.SourceStatus);
            Assert.Equal("dreadnaught-battlegear",item.SetKey);
            Assert.Equal("plate",item.ItemSubclass);
            Assert.Equal("warrior",Assert.Single(item.AllowedClasses));
        });
        var loadout=new CharacterEquipmentLoadout {
            Items=Pieces.Select(x=>new CharacterEquipmentItem {
                SlotKey=x.Slot,ItemId=x.Id
            }).ToList()
        };
        var resolved=ForeverSharedEquipmentResolver.Resolve(loadout,"warrior",60,"human");
        Assert.Equal(8,resolved.Loadout.Items.Count);
        Assert.Equal(items.Sum(x=>x.Stats["strength"]),resolved.RawStats.Get("strength"));
        Assert.Equal(items.Sum(x=>x.Stats["stamina"]),resolved.RawStats.Get("stamina"));
        Assert.Equal(items.Sum(x=>x.Stats["armor"]),resolved.RawStats.Get("armor"));
        Assert.Equal(items.Sum(x=>x.Stats.GetValueOrDefault("defense-rating")),
            resolved.RawStats.Get("defense-rating"));
        Assert.Throws<ArgumentException>(()=>
            ForeverSharedEquipmentResolver.Resolve(loadout,"mage",60,"human"));
    }

    [Fact]
    public void KnownUnsupportedSetAndBlockEffectsRemainNonExecutable()
    {
        var catalog=ForeverEquipmentCatalog.GetCatalog();
        var set=catalog.Sets.Where(x=>x.Key=="dreadnaught-battlegear").ToArray();
        Assert.Equal(4,set.Length);
        Assert.All(set,s=>{
            Assert.False(s.Modeled);
            Assert.Empty(s.Stats);
            Assert.NotEmpty(s.Notes);
        });
        Assert.Contains(ForeverEquipmentCatalog.FindItem(22422).Effects,e=>
            e.Kind=="unsupported" && e.ExecutionStatus=="not-modeled");
        Assert.Contains(ForeverEquipmentCatalog.FindItem(22417).Effects,e=>
            e.Kind=="unsupported" && e.ExecutionStatus=="not-modeled");
    }

    [Fact]
    public void ChoosingAReviewedAlternativeChangesTheResolvedCombatStats()
    {
        CharacterEquipmentLoadout Load(int id)=>new(){
            Items=[new CharacterEquipmentItem{SlotKey="chest",ItemId=id}]
        };
        var old=ForeverSharedEquipmentResolver.Resolve(Load(11726),"warrior",60,"human");
        var current=ForeverSharedEquipmentResolver.Resolve(Load(22416),"warrior",60,"human");
        Assert.True(current.RawStats.Get("stamina")>old.RawStats.Get("stamina"));
        Assert.True(current.RawStats.Get("armor")>old.RawStats.Get("armor"));
    }
}
