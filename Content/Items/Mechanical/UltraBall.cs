﻿using Microsoft.Xna.Framework;
using Terraria.GameContent.Creative;

namespace Terramon.Content.Items.Mechanical;

internal class UltraBallProjectile : BasePkballProjectile
{
    public override int pokeballCapture => ModContent.ItemType<UltraBallItem>();
    public override float catchModifier => 2f;
}

internal class UltraBallItem : BasePkballItem
{
    protected override int UseRarity => ModContent.RarityType<UltraBallRarity>();
    protected override int pokeballThrow => ModContent.ProjectileType<UltraBallProjectile>();
    protected override int pokeballTile => ModContent.TileType<UltraBallTile>();
    protected override int igPrice => 800;

    public override void SetStaticDefaults()
    {
        CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] =
            igPrice / 2; //Amount needed to duplicate them in Journey Mode
    }
}

public class UltraBallTile : BasePkballTile
{
    protected override int dropItem => ModContent.ItemType<UltraBallItem>();
}

public class UltraBallRarity : ModRarity
{
    public override Color RarityColor => new(249, 182, 27);
}