using System.Linq;
using Terramon.Content.Buffs;
using Terramon.Content.GUI;
using Terramon.Content.Items.PokeBalls;
using Terramon.Content.Packets;
using Terramon.Core.Loaders.UILoading;
using Terramon.Core.Networking;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace Terramon.Core;

public class TerramonPlayer : ModPlayer
{
    private readonly PCService _pc = new();
    private readonly PokedexService _pokedex = new();

    private int _activeSlot = -1;

    private bool _lastPlayerInventory;
    private int _premierBonusCount;

    public bool HasChosenStarter;
    public PokemonData[] Party { get; } = new PokemonData[6];

    public int ActiveSlot
    {
        get => _activeSlot;
        set
        {
            // Toggle off dedicated pet slot
            if (_activeSlot == -1)
                Player.hideMisc[0] = true;
            _activeSlot = value;
            var buffType = ModContent.BuffType<PokemonCompanion>();
            Player.ClearBuff(buffType);
            if (value >= 0) Player.AddBuff(buffType, 2);
        }
    }

    public static TerramonPlayer LocalPlayer => Main.LocalPlayer.GetModPlayer<TerramonPlayer>();

    /// <summary>
    ///     Returns this player's currently active Pokémon, or null if there is none.
    /// </summary>
    public PokemonData GetActivePokemon()
    {
        return ActiveSlot >= 0 ? Party[ActiveSlot] : null;
    }

    public PokedexService GetPokedex()
    {
        return _pokedex;
    }

    public override void OnEnterWorld()
    {
        UILoader.GetUIState<PartyDisplay>().UpdateAllSlots(Party);
    }

    public override void OnRespawn()
    {
        if (Player.whoAmI != Main.myPlayer) return;

        // Reapply companion buff on player respawn
        if (ActiveSlot >= 0)
            Player.AddBuff(ModContent.BuffType<PokemonCompanion>(), 2);
    }

    public override void PreUpdate()
    {
        if (Player.whoAmI != Main.myPlayer) return;

        // Handle player removing companion buff manually (right-clicking the buff icon)
        if (!Player.HasBuff<PokemonCompanion>() && ActiveSlot >= 0 && !Player.dead)
        {
            var oldSlot = ActiveSlot;
            ActiveSlot = -1;
            UILoader.GetUIState<PartyDisplay>().RecalculateSlot(oldSlot);
        }

        if (Main.playerInventory && !_lastPlayerInventory)
            UILoader.GetUIState<PartyDisplay>().Sidebar.ForceKillAnimation();

        _lastPlayerInventory = Main.playerInventory;

        if (_premierBonusCount <= 0 || Main.npcShop != 0) return;
        var premierBonus = _premierBonusCount / 10;
        if (premierBonus > 0)
        {
            Main.NewText(premierBonus == 1
                ? Language.GetTextValue("Mods.Terramon.GUI.Shop.PremierBonus")
                : Language.GetTextValue("Mods.Terramon.GUI.Shop.PremierBonusPlural", premierBonus));

            Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), ModContent.ItemType<PremierBallItem>(),
                premierBonus);
        }

        _premierBonusCount = 0;
    }

    public override bool CanSellItem(NPC vendor, Item[] shopInventory, Item item)
    {
        //use CanSellItem rather than PostSellItem because PostSellItem doesn't return item data correctly
        if (item.type == ModContent.ItemType<PokeBallItem>())
            _premierBonusCount -= item.stack;

        return item.type != ModContent.ItemType<MasterBallItem>() && base.CanSellItem(vendor, shopInventory, item);
    }

    public override void PostBuyItem(NPC vendor, Item[] shopInventory, Item item)
    {
        if (item.type == ModContent.ItemType<PokeBallItem>())
            _premierBonusCount++;
    }

    /// <summary>
    ///     Adds a Pokémon to the player's party. Returns false if their party is full; otherwise returns true.
    /// </summary>
    public bool AddPartyPokemon(PokemonData data, bool addToPokedex = true)
    {
        if (addToPokedex)
            UpdatePokedex(data.ID, PokedexEntryStatus.Registered);

        var nextIndex = NextFreePartyIndex();
        if (nextIndex == 6) return false;
        Party[nextIndex] = data;
        UILoader.GetUIState<PartyDisplay>().UpdateSlot(data, nextIndex);

        return true;
    }

    public void SwapParty(int first, int second)
    {
        if (ActiveSlot == first)
            ActiveSlot = second;
        else if (ActiveSlot == second)
            ActiveSlot = first;
        (Party[first], Party[second]) = (Party[second], Party[first]);
    }

    public int NextFreePartyIndex()
    {
        for (var i = 0; i < Party.Length; i++)
            if (Party[i] == null)
                return i;

        return 6;
    }

    public bool UpdatePokedex(ushort id, PokedexEntryStatus status)
    {
        var containsId = _pokedex.Entries.ContainsKey(id);
        if (containsId) _pokedex.Entries[id] = status;
        return containsId;
    }

    public PCBox TransferPokemonToPC(PokemonData data)
    {
        return _pc.StorePokemon(data);
    }

    public string GetDefaultNameForPCBox(PCBox box)
    {
        return "Box " + (_pc.Boxes.IndexOf(box) + 1);
    }

    public override void SaveData(TagCompound tag)
    {
        tag["starterChosen"] = HasChosenStarter;
        if (ActiveSlot >= 0)
            tag["activeSlot"] = ActiveSlot;
        SaveParty(tag);
        SavePokedex(tag);
        SavePC(tag);
    }

    public override void LoadData(TagCompound tag)
    {
        HasChosenStarter = tag.GetBool("starterChosen");
        if (tag.TryGet("activeSlot", out int slot))
            ActiveSlot = slot;
        LoadParty(tag);
        LoadPokedex(tag);
        LoadPC(tag);
    }

    private void SaveParty(TagCompound tag)
    {
        for (var i = 0; i < Party.Length; i++)
        {
            if (Party[i] == null) continue;
            tag[$"p{i}"] = Party[i];
        }
    }

    private void LoadParty(TagCompound tag)
    {
        for (var i = 0; i < Party.Length; i++)
        {
            var tagName = $"p{i}";
            if (tag.ContainsKey(tagName)) Party[i] = tag.Get<PokemonData>(tagName);
        }
    }

    private void SavePokedex(TagCompound tag)
    {
        tag["pokedex"] = _pokedex.Entries.Select(entry => new[] { entry.Key, (byte)entry.Value }).ToList();
    }

    private void LoadPokedex(TagCompound tag)
    {
        const string tagName = "pokedex";
        if (!tag.ContainsKey(tagName)) return;
        var entries = tag.GetList<int[]>(tagName);
        foreach (var entry in entries) UpdatePokedex((ushort)entry[0], (PokedexEntryStatus)entry[1]);
    }

    private void SavePC(TagCompound tag)
    {
        tag["pc"] = _pc.Boxes;
    }

    private void LoadPC(TagCompound tag)
    {
        const string tagName = "pc";
        if (!tag.ContainsKey(tagName)) return;
        _pc.Boxes.Clear();
        var boxes = tag.GetList<PCBox>(tagName);
        foreach (var box in boxes)
        {
            box.Service = _pc;
            _pc.Boxes.Add(box);
        }
    }

    #region Network Sync

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        for (var i = 0; i < Party.Length; i++)
            Mod.SendPacket(new PartySyncPacket((byte)Player.whoAmI, (byte)i, Party[i]), toWho, fromWho, !newPlayer);
        Mod.SendPacket(new ActiveSlotUpdatedPacket((byte)Player.whoAmI, ActiveSlot), toWho, fromWho, !newPlayer);
    }

    public override void CopyClientState(ModPlayer targetCopy)
    {
        var clone = (TerramonPlayer)targetCopy;
        for (var i = 0; i < Party.Length; i++)
            if (clone.Party[i] != null && Party[i] == null)
                clone.Party[i] = null;
            else if (clone.Party[i] == null && Party[i] != null)
                clone.Party[i] = Party[i].ShallowCopy();
        /*else if (clone.Party[i] != null && Party[i] != null)
                Party[i].CopyNetStateTo(clone.Party[i]);*/
        clone.ActiveSlot = ActiveSlot;
    }

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        var clone = (TerramonPlayer)clientPlayer;
        for (var i = 0; i < Party.Length; i++)
            if ((Party[i] == null && clone.Party[i] != null) || (Party[i] != null && clone.Party[i] == null))
                Mod.SendPacket(new PartySyncPacket((byte)Player.whoAmI, (byte)i, Party[i]), -1, Main.myPlayer, true);
        if (ActiveSlot == clone.ActiveSlot) return;
        Mod.SendPacket(new ActiveSlotUpdatedPacket((byte)Player.whoAmI, ActiveSlot), -1, Main.myPlayer, true);
    }

    #endregion
}