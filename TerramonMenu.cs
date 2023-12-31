﻿using ReLogic.Content;

namespace Terramon;

public class TerramonMenu : ModMenu
{
    public override string DisplayName => "Terramon";

    public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Audio/Music/Menu");

    public override Asset<Texture2D> Logo =>
        ModContent.Request<Texture2D>("Terramon/logo", AssetRequestMode.ImmediateLoad);
}