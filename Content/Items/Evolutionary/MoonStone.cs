using Terramon.Content.Rarities;

namespace Terramon.Content.Items.Evolutionary;

public class MoonStone : EvolutionaryItem
{
    protected override int UseRarity => ModContent.RarityType<MoonStoneRarity>();

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = 28;
        Item.height = 28;
    }
}

public class MoonStoneRarity : DiscoRarity
{
    protected override Color[] Colors { get; } =
    {
        new(154, 170, 124),
        new(97, 110, 74)
    };

    protected override float Time => 2f;
}