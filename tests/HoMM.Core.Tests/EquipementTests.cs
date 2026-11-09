using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>L'équipement du héros (écran du héros refait, 2026-11-09) : 11 emplacements et un coffre de 16 places.</summary>
public class EquipementTests
{
    private static ArtifactType Artifact(string id, ArtifactSlot slot) => new() { Id = id, Name = id, Slot = slot };

    [Fact]
    public void Onze_emplacements_dont_deux_anneaux()
    {
        Assert.Equal(11, Equipment.Slots.Length);
        Assert.Equal(2, Equipment.Slots.Count(s => s == ArtifactSlot.Ring));
        Assert.Equal(16, Equipment.ChestSize);
    }

    [Fact]
    public void Chaque_artefact_va_a_sa_place_et_le_reste_au_coffre()
    {
        var crown = Artifact("couronne", ArtifactSlot.Head);
        var ring1 = Artifact("anneau-1", ArtifactSlot.Ring);
        var ring2 = Artifact("anneau-2", ArtifactSlot.Ring);
        var ring3 = Artifact("anneau-3", ArtifactSlot.Ring);
        var helmet = Artifact("casque", ArtifactSlot.Head);

        var (worn, chest) = Equipment.Arrange(new[] { crown, ring1, ring2, ring3, helmet });

        Assert.Same(crown, worn[System.Array.IndexOf(Equipment.Slots, ArtifactSlot.Head)]);
        Assert.Equal(new[] { ring1, ring2 }, worn.Where((_, i) => Equipment.Slots[i] == ArtifactSlot.Ring));
        Assert.Equal(new[] { ring3, helmet }, chest);
    }

    [Fact]
    public void Le_tresor_se_porte_sur_le_torse()
    {
        Assert.Equal(ArtifactSlot.Torso, TestData.Data.Artifacts.Treasure!.Slot);
    }
}
