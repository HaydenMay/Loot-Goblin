using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// V1 is intentionally limited to the three currently authored Iron assets. Definitions are
// shared data; only OwnedEquipmentItem instances are saved, rolled, compared, and equipped.
public enum EquipmentSlot { Weapon, Head, Chest }
public enum EquipmentRarity { Common, Uncommon, Rare, Epic }
public enum EquipmentSort { Power, Rarity, Newest, UpgradeLevel }
public enum EquipmentPerkType { Damage, CritChance, Impact, RecoverySpeed, Health }

[Serializable]
public sealed class EquipmentModifier
{
    public EquipmentPerkType type;
    public float value;
}

[Serializable]
public sealed class OwnedEquipmentItem
{
    public string instanceId;
    public string definitionId;
    public EquipmentRarity rarity;
    public int power;
    public int upgradeLevel;
    public List<EquipmentModifier> modifiers = new();
    public bool equipped;
    public bool isNew;
    public bool inRunBag;
    public long acquiredOrder;
}

public sealed class EquipmentDefinition
{
    public readonly string Id;
    public readonly string DisplayName;
    public readonly EquipmentSlot Slot;
    public readonly string Family;
    public readonly string ExistingVisualName;
    public readonly string IconGlyph;
    public readonly EquipmentModifier[] BaseStats;

    public EquipmentDefinition(string id, string displayName, EquipmentSlot slot, string existingVisualName, string iconGlyph, params EquipmentModifier[] baseStats)
    {
        Id = id;
        DisplayName = displayName;
        Slot = slot;
        Family = "Iron";
        ExistingVisualName = existingVisualName;
        IconGlyph = iconGlyph;
        BaseStats = baseStats;
    }
}

// One tunable, centralized browsing score. It deliberately is not combat damage.
public static class EquipmentPower
{
    public static int Calculate(EquipmentDefinition definition, EquipmentRarity rarity, int upgradeLevel, IEnumerable<EquipmentModifier> modifiers)
    {
        float score = definition.Slot switch { EquipmentSlot.Weapon => 68f, EquipmentSlot.Head => 48f, _ => 58f };
        score += (int)rarity * 20f + upgradeLevel * 8f;
        foreach (EquipmentModifier modifier in modifiers ?? Array.Empty<EquipmentModifier>())
            score += modifier.type switch
            {
                EquipmentPerkType.Damage => modifier.value * 7f,
                EquipmentPerkType.CritChance => modifier.value * 5f,
                EquipmentPerkType.Impact => modifier.value * 2.5f,
                EquipmentPerkType.RecoverySpeed => modifier.value * 18f,
                EquipmentPerkType.Health => modifier.value * .6f,
                _ => 0f
            };
        return Mathf.RoundToInt(score);
    }
}

// Shared model visibility path for gameplay and the cloned Equipment preview. It only toggles
// the existing scene-attached Sword, Helmet, and Armor model instances; no equipment prefab is made.
public sealed class LootGoblinEquipmentVisuals
{
    static readonly Dictionary<EquipmentSlot, string> SocketNames = new()
    {
        { EquipmentSlot.Weapon, "WeaponSocket" },
        { EquipmentSlot.Head, "HeadSocket" },
        { EquipmentSlot.Chest, "ChestSocket" }
    };

    public void Apply(Transform goblinRoot, IReadOnlyDictionary<EquipmentSlot, OwnedEquipmentItem> equipped)
    {
        if (goblinRoot == null) return;
        foreach (var pair in SocketNames)
        {
            Transform socket = Find(goblinRoot, pair.Value);
            if (socket == null) continue;
            // V1 has one visual per Iron definition, but the item instance still controls whether
            // the slot is present. Future definitions add their visual identity here, not a new path.
            socket.gameObject.SetActive(equipped.TryGetValue(pair.Key, out OwnedEquipmentItem item) && item != null);
        }
    }

    static Transform Find(Transform root, string targetName)
    {
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            if (candidate.name == targetName) return candidate;
        return null;
    }
}

[DisallowMultipleComponent]
public sealed class LootGoblinEquipment : MonoBehaviour
{
    static readonly EquipmentDefinition[] Definitions =
    {
        new("iron-sword", "Iron Sword", EquipmentSlot.Weapon, "Sword", "⚔",
            new EquipmentModifier { type = EquipmentPerkType.Damage, value = 8 }, new EquipmentModifier { type = EquipmentPerkType.Impact, value = 5 }),
        new("iron-head", "Iron Head", EquipmentSlot.Head, "Helmet", "⌁",
            new EquipmentModifier { type = EquipmentPerkType.Health, value = 16 }),
        new("iron-chest", "Iron Chest", EquipmentSlot.Chest, "Armor", "▣",
            new EquipmentModifier { type = EquipmentPerkType.Health, value = 24 })
    };

    const int PreviewLayer = 30;
    readonly LootGoblinEquipmentVisuals visuals = new();
    readonly Dictionary<EquipmentSlot, OwnedEquipmentItem> equipped = new();
    LootGoblinRun run;
    LootGoblinSaveData save;
    Transform player;
    GameObject previewRoot;
    Camera previewCamera;
    RenderTexture previewTexture;
    EquipmentSlot selectedSlot = EquipmentSlot.Weapon;
    EquipmentSort sort = EquipmentSort.Power;
    bool descending = true;
    bool filterOpen;
    bool filterEquippedOnly;
    bool filterNewOnly;
    int rarityFilter = -1;
    string selectedItemId;
    int nextDropSlot;

    public bool IsOpen { get; private set; }
    public int DamageBonus => Mathf.RoundToInt(Sum(EquipmentPerkType.Damage));
    public int HealthBonus => Mathf.RoundToInt(Sum(EquipmentPerkType.Health));
    public float CritChanceBonus => Sum(EquipmentPerkType.CritChance);
    public int ImpactBonus => Mathf.RoundToInt(Sum(EquipmentPerkType.Impact));
    public float RecoverySpeedBonus => Sum(EquipmentPerkType.RecoverySpeed);
    public int IronFamilyCount => equipped.Values.Count(item => item != null && Definition(item.definitionId)?.Family == "Iron");
    public int RunBagCount => save.ownedEquipment.Count(item => item.inRunBag && !item.equipped);
    public bool HasActivePreview => previewRoot != null && previewRoot.activeSelf && previewCamera != null && previewTexture != null;
    public IReadOnlyList<OwnedEquipmentItem> OwnedItems => save.ownedEquipment;
    public event Action StateChanged;

    void Awake()
    {
        run = GetComponent<LootGoblinRun>();
        player = run != null ? run.Player : null;
        save = LootGoblinSave.Load();
        SeedFirstSave();
        RebuildEquipped();
        ApplyState();
    }

    void OnDestroy()
    {
        if (previewTexture != null) previewTexture.Release();
        if (previewRoot != null) Destroy(previewRoot);
        if (previewCamera != null) Destroy(previewCamera.gameObject);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        CreatePreviewIfNeeded();
        previewRoot.SetActive(true);
        ApplyPreview();
    }

    public void Close()
    {
        IsOpen = false;
        filterOpen = false;
        if (previewRoot != null) previewRoot.SetActive(false);
    }

    public void GrantRunDrop()
    {
        EquipmentSlot slot = (EquipmentSlot)(nextDropSlot++ % 3);
        int count = save.ownedEquipment.Count + nextDropSlot;
        EquipmentRarity rarity = count % 5 == 0 ? EquipmentRarity.Rare : count % 2 == 0 ? EquipmentRarity.Uncommon : EquipmentRarity.Common;
        int upgrade = count % 4 == 0 ? 1 : 0;
        var item = new OwnedEquipmentItem
        {
            instanceId = Guid.NewGuid().ToString("N"),
            definitionId = DefinitionForSlot(slot).Id,
            rarity = rarity,
            upgradeLevel = upgrade,
            modifiers = RollModifiers(slot, rarity, count),
            isNew = true,
            inRunBag = true,
            acquiredOrder = DateTime.UtcNow.Ticks
        };
        item.power = EquipmentPower.Calculate(Definition(item.definitionId), item.rarity, item.upgradeLevel, item.modifiers);
        save.ownedEquipment.Add(item);
        Persist();
    }

    // Extraction has a narrow integration hook: callers may subscribe to each conversion once an
    // economy owns Scrap. V1 deliberately does not create a parallel currency system.
    public event Action<OwnedEquipmentItem> RunBagItemConvertedToScrap;
    public void CommitExtraction()
    {
        foreach (OwnedEquipmentItem item in save.ownedEquipment.Where(item => item.inRunBag && !item.equipped).ToArray())
        {
            RunBagItemConvertedToScrap?.Invoke(item);
            save.ownedEquipment.Remove(item);
        }
        Persist();
    }

    public void DiscardRunBagOnDeath()
    {
        save.ownedEquipment.RemoveAll(item => item.inRunBag && !item.equipped);
        Persist();
    }

    public void Equip(string instanceId)
    {
        OwnedEquipmentItem item = save.ownedEquipment.FirstOrDefault(candidate => candidate.instanceId == instanceId);
        EquipmentDefinition definition = item == null ? null : Definition(item.definitionId);
        if (definition == null) return;

        if (equipped.TryGetValue(definition.Slot, out OwnedEquipmentItem replaced) && replaced != item)
        {
            replaced.equipped = false;
            // The safe/on-body rule applies only while a run is active. Safe inventory swaps at Home
            // remain persistent inventory, rather than being incorrectly marked as at risk.
            replaced.inRunBag = run != null && !run.IsMainMenu;
        }
        item.equipped = true;
        item.inRunBag = false;
        item.isNew = false;
        RebuildEquipped();
        ApplyState();
        Persist();
    }

    public OwnedEquipmentItem GetEquipped(EquipmentSlot slot) => equipped.TryGetValue(slot, out OwnedEquipmentItem item) ? item : null;

    public IReadOnlyList<OwnedEquipmentItem> GetSortedItems(EquipmentSlot slot, EquipmentSort criterion, bool descendingOrder)
    {
        IEnumerable<OwnedEquipmentItem> query = save.ownedEquipment.Where(item => Definition(item.definitionId)?.Slot == slot);
        query = criterion switch
        {
            EquipmentSort.Rarity => query.OrderBy(item => item.rarity),
            EquipmentSort.Newest => query.OrderBy(item => item.acquiredOrder),
            EquipmentSort.UpgradeLevel => query.OrderBy(item => item.upgradeLevel),
            _ => query.OrderBy(item => item.power)
        };
        return (descendingOrder ? query.Reverse() : query).ToList();
    }

    void SeedFirstSave()
    {
        if (save.ownedEquipment.Count > 0) return;
        foreach (EquipmentDefinition definition in Definitions)
        {
            var item = new OwnedEquipmentItem
            {
                instanceId = Guid.NewGuid().ToString("N"),
                definitionId = definition.Id,
                rarity = EquipmentRarity.Common,
                upgradeLevel = 0,
                equipped = true,
                isNew = false,
                acquiredOrder = DateTime.UtcNow.Ticks
            };
            item.modifiers = definition.Slot == EquipmentSlot.Weapon
                ? new List<EquipmentModifier> { new() { type = EquipmentPerkType.Damage, value = 2 } }
                : new List<EquipmentModifier>();
            item.power = EquipmentPower.Calculate(definition, item.rarity, item.upgradeLevel, item.modifiers);
            save.ownedEquipment.Add(item);
        }
        Persist();
    }

    static List<EquipmentModifier> RollModifiers(EquipmentSlot slot, EquipmentRarity rarity, int seed)
    {
        var result = new List<EquipmentModifier>();
        if (slot == EquipmentSlot.Weapon)
        {
            result.Add(new EquipmentModifier { type = EquipmentPerkType.Damage, value = 3 + seed % 4 });
            if (rarity >= EquipmentRarity.Uncommon) result.Add(new EquipmentModifier { type = EquipmentPerkType.CritChance, value = 2 + seed % 4 });
        }
        else if (slot == EquipmentSlot.Head)
        {
            result.Add(new EquipmentModifier { type = EquipmentPerkType.CritChance, value = 1 + seed % 3 });
            if (rarity >= EquipmentRarity.Rare) result.Add(new EquipmentModifier { type = EquipmentPerkType.RecoverySpeed, value = .04f });
        }
        else
        {
            result.Add(new EquipmentModifier { type = EquipmentPerkType.Health, value = 8 + seed % 8 });
            if (rarity >= EquipmentRarity.Uncommon) result.Add(new EquipmentModifier { type = EquipmentPerkType.Impact, value = 2 + seed % 3 });
        }
        return result;
    }

    void RebuildEquipped()
    {
        equipped.Clear();
        foreach (OwnedEquipmentItem item in save.ownedEquipment)
        {
            EquipmentDefinition definition = Definition(item.definitionId);
            if (item.equipped && definition != null) equipped[definition.Slot] = item;
        }
    }

    void ApplyState()
    {
        visuals.Apply(player, equipped);
        if (player != null) player.GetComponent<PlayerHealth>()?.SetEquipmentHealthBonus(HealthBonus);
        ApplyPreview();
        StateChanged?.Invoke();
    }

    float Sum(EquipmentPerkType type)
    {
        float value = 0;
        foreach (OwnedEquipmentItem item in equipped.Values)
        {
            EquipmentDefinition definition = Definition(item.definitionId);
            foreach (EquipmentModifier modifier in definition.BaseStats) if (modifier.type == type) value += modifier.value;
            foreach (EquipmentModifier modifier in item.modifiers) if (modifier.type == type) value += modifier.value;
        }
        return value;
    }

    void Persist()
    {
        LootGoblinSave.Save(save);
    }

    // Used by the editor smoke fixture after it restores the user's PlayerPrefs payload.
    public void ReloadFromPersistence()
    {
        save = LootGoblinSave.Load();
        SeedFirstSave();
        RebuildEquipped();
        ApplyState();
    }

    public static EquipmentDefinition Definition(string id) => Definitions.FirstOrDefault(definition => definition.Id == id);
    static EquipmentDefinition DefinitionForSlot(EquipmentSlot slot) => Definitions.First(definition => definition.Slot == slot);

    void CreatePreviewIfNeeded()
    {
        if (previewRoot != null || player == null) return;
        previewRoot = Instantiate(player.gameObject);
        previewRoot.name = "Equipment Preview Goblin (Runtime)";
        previewRoot.hideFlags = HideFlags.DontSave;
        previewRoot.transform.position = new Vector3(0, -1000, 0);
        previewRoot.transform.rotation = Quaternion.Euler(0, 180, 0);
        foreach (MonoBehaviour behaviour in previewRoot.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        SetLayerRecursively(previewRoot.transform, PreviewLayer);

        var cameraRoot = new GameObject("Equipment Preview Camera (Runtime)");
        cameraRoot.hideFlags = HideFlags.DontSave;
        previewCamera = cameraRoot.AddComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 2.15f;
        previewCamera.nearClipPlane = .1f;
        previewCamera.farClipPlane = 20f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(.035f, .055f, .09f, 1f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.transform.position = new Vector3(0, -998.75f, -5f);
        previewCamera.transform.LookAt(new Vector3(0, -999.1f, 0));
        previewTexture = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32) { name = "Equipment Preview RT", hideFlags = HideFlags.DontSave };
        previewCamera.targetTexture = previewTexture;
        previewCamera.enabled = false;
    }

    void ApplyPreview()
    {
        if (previewRoot == null) return;
        visuals.Apply(previewRoot.transform, equipped);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    void OnGUI()
    {
        if (run == null) return;
        if (IsOpen) DrawEquipmentScreen();
        else DrawNavigation();
    }

    void DrawNavigation()
    {
        float scale = Scale();
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        Rect safe = Safe(scale);
        GUIStyle nav = new(GUI.skin.button) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
        if (run.IsMainMenu)
        {
            float y = safe.yMax - 70;
            float width = safe.width / 3f;
            if (GUI.Button(new Rect(safe.x, y, width, 58), "EQUIPMENT", nav)) Open();
            GUI.Box(new Rect(safe.x + width, y, width, 58), "HOME", nav);
            if (GUI.Button(new Rect(safe.x + width * 2, y, width, 58), "UPGRADES", nav))
                GUI.Box(new Rect(safe.x + 42, y - 68, safe.width - 84, 52), "Upgrades are intentionally deferred for Equipment V1.");
        }
        else if (!run.Failed && !run.DepthComplete && GUI.Button(new Rect(safe.xMax - 122, safe.y + 70, 112, 34), "EQUIPMENT", nav)) Open();
    }

    void DrawEquipmentScreen()
    {
        float scale = Scale();
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        Rect safe = Safe(scale);
        GUIStyle title = new(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        GUIStyle small = new(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.78f, .86f, 1f) } };
        GUI.Box(safe, GUIContent.none);
        if (GUI.Button(new Rect(safe.x + 10, safe.y + 8, 72, 30), "‹ HOME")) { Close(); return; }
        GUI.Label(new Rect(safe.x + 80, safe.y + 6, safe.width - 160, 35), "EQUIPMENT", title);

        Rect previewRect = new(safe.x + 12, safe.y + 46, safe.width * .45f, 220);
        if (previewCamera != null) { previewCamera.Render(); GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, false); }
        DrawSlotButton(new Rect(safe.x + safe.width * .48f, safe.y + 58, safe.width * .48f, 42), EquipmentSlot.Weapon);
        DrawSlotButton(new Rect(safe.x + safe.width * .48f, safe.y + 108, safe.width * .48f, 42), EquipmentSlot.Head);
        DrawSlotButton(new Rect(safe.x + safe.width * .48f, safe.y + 158, safe.width * .48f, 42), EquipmentSlot.Chest);
        GUI.Label(new Rect(previewRect.x, previewRect.yMax - 26, previewRect.width, 25), $"IRON SET  {IronFamilyCount}/3", small);

        GUI.Box(new Rect(safe.x + 12, safe.y + 272, safe.width - 24, 48), $"POWER  {TotalPower()}     DAMAGE  {DamageBonus}     HP  {100 + HealthBonus}");
        float controlsY = safe.y + 327;
        if (GUI.Button(new Rect(safe.x + 12, controlsY, 74, 32), "FILTER")) filterOpen = !filterOpen;
        if (GUI.Button(new Rect(safe.x + 92, controlsY, 110, 32), SortLabel())) sort = (EquipmentSort)(((int)sort + 1) % 4);
        if (GUI.Button(new Rect(safe.x + 208, controlsY, 48, 32), descending ? "↓" : "↑")) descending = !descending;
        GUI.Label(new Rect(safe.x + 262, controlsY, safe.width - 274, 32), $"{FilteredItems().Count} owned", small);

        DrawGrid(new Rect(safe.x + 12, safe.y + 365, safe.width - 24, safe.height - 440));
        if (filterOpen) DrawFilter(safe);
        if (!string.IsNullOrEmpty(selectedItemId)) DrawDrawer(safe);
    }

    void DrawSlotButton(Rect rect, EquipmentSlot slot)
    {
        EquipmentDefinition definition = DefinitionForSlot(slot);
        OwnedEquipmentItem item = equipped.TryGetValue(slot, out OwnedEquipmentItem value) ? value : null;
        string caption = $"{definition.IconGlyph}  {slot}\n{(item == null ? "Empty" : item.power + " Power")}";
        GUIStyle style = new(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        if (selectedSlot == slot) style.normal.textColor = new Color(.25f, .7f, 1f);
        if (GUI.Button(rect, caption, style)) { selectedSlot = slot; selectedItemId = null; }
    }

    void DrawGrid(Rect area)
    {
        List<OwnedEquipmentItem> items = FilteredItems();
        const int columns = 4;
        float gap = 7;
        float tile = (area.width - gap * (columns - 1)) / columns;
        for (int index = 0; index < items.Count; index++)
        {
            int row = index / columns;
            int col = index % columns;
            Rect tileRect = new(area.x + col * (tile + gap), area.y + row * (tile + 12), tile, tile);
            OwnedEquipmentItem item = items[index];
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = RarityColor(item.rarity);
            if (GUI.Button(tileRect, $"{Definition(item.definitionId).IconGlyph}\n\n{item.power}", new GUIStyle(GUI.skin.button) { fontSize = 21, alignment = TextAnchor.MiddleCenter })) Inspect(item);
            GUI.backgroundColor = old;
            if (item.equipped) GUI.Label(new Rect(tileRect.x + 2, tileRect.y + 2, tileRect.width - 4, 18), "EQUIPPED", new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.cyan } });
            else if (item.isNew) GUI.Label(new Rect(tileRect.x + 2, tileRect.y + 2, tileRect.width - 4, 18), "NEW", new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(1f, .45f, .35f) } });
        }
    }

    void DrawFilter(Rect safe)
    {
        Rect panel = new(safe.x + 28, safe.y + 135, safe.width - 56, 175);
        GUI.Box(panel, "FILTER");
        filterEquippedOnly = GUI.Toggle(new Rect(panel.x + 16, panel.y + 35, panel.width - 32, 24), filterEquippedOnly, " Equipped only");
        filterNewOnly = GUI.Toggle(new Rect(panel.x + 16, panel.y + 63, panel.width - 32, 24), filterNewOnly, " New only");
        if (GUI.Button(new Rect(panel.x + 16, panel.y + 94, panel.width - 32, 28), rarityFilter < 0 ? "Rarity: Any" : "Rarity: " + (EquipmentRarity)rarityFilter)) rarityFilter = rarityFilter >= 3 ? -1 : rarityFilter + 1;
        if (GUI.Button(new Rect(panel.x + 16, panel.y + 130, panel.width - 32, 28), "DONE")) filterOpen = false;
    }

    void DrawDrawer(Rect safe)
    {
        OwnedEquipmentItem item = save.ownedEquipment.FirstOrDefault(candidate => candidate.instanceId == selectedItemId);
        if (item == null) { selectedItemId = null; return; }
        EquipmentDefinition definition = Definition(item.definitionId);
        OwnedEquipmentItem current = equipped.TryGetValue(definition.Slot, out OwnedEquipmentItem value) ? value : null;
        int difference = item.power - (current == null ? 0 : current.power);
        Rect panel = new(safe.x + 8, safe.yMax - 245, safe.width - 16, 237);
        GUI.Box(panel, GUIContent.none);
        GUIStyle heading = new(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        GUI.Label(new Rect(panel.x + 16, panel.y + 12, panel.width - 90, 28), definition.DisplayName, heading);
        GUI.Label(new Rect(panel.x + 16, panel.y + 42, panel.width - 32, 22), $"{item.rarity} · Iron Family · +{item.upgradeLevel}");
        GUI.Label(new Rect(panel.x + 16, panel.y + 70, panel.width - 32, 22), $"Power  {item.power}  ({Signed(difference)})");
        float line = panel.y + 98;
        foreach (EquipmentModifier modifier in CombinedStats(definition, item).Take(3))
        {
            float previous = current == null ? 0 : Stat(current, modifier.type);
            GUI.Label(new Rect(panel.x + 16, line, panel.width - 32, 21), $"{Pretty(modifier.type)}  {PrettyValue(modifier)}  ({Signed(modifier.value - previous)})");
            line += 22;
        }
        if (item.modifiers.Count > 0) GUI.Label(new Rect(panel.x + 16, line + 2, panel.width - 32, 20), $"Perk: {Pretty(item.modifiers[0].type)} +{PrettyValue(item.modifiers[0])}");
        string equipText = item.equipped ? "EQUIPPED" : "EQUIP";
        if (GUI.Button(new Rect(panel.x + 16, panel.yMax - 48, panel.width - 32, 36), equipText) && !item.equipped) Equip(item.instanceId);
    }

    void Inspect(OwnedEquipmentItem item)
    {
        selectedItemId = item.instanceId;
        if (item.isNew) { item.isNew = false; Persist(); }
    }

    List<OwnedEquipmentItem> FilteredItems()
    {
        IEnumerable<OwnedEquipmentItem> query = save.ownedEquipment.Where(item => Definition(item.definitionId)?.Slot == selectedSlot);
        if (filterEquippedOnly) query = query.Where(item => item.equipped);
        if (filterNewOnly) query = query.Where(item => item.isNew);
        if (rarityFilter >= 0) query = query.Where(item => (int)item.rarity == rarityFilter);
        List<OwnedEquipmentItem> sorted = GetSortedItems(selectedSlot, sort, descending).ToList();
        return sorted.Where(item => query.Contains(item)).ToList();
    }

    int TotalPower() => equipped.Values.Sum(item => item.power);
    static IEnumerable<EquipmentModifier> CombinedStats(EquipmentDefinition definition, OwnedEquipmentItem item) => definition.BaseStats.Concat(item.modifiers);
    static float Stat(OwnedEquipmentItem item, EquipmentPerkType type) => CombinedStats(Definition(item.definitionId), item).Where(stat => stat.type == type).Sum(stat => stat.value);
    string SortLabel() => sort switch
    {
        EquipmentSort.Rarity => "RARITY",
        EquipmentSort.Newest => "NEWEST",
        EquipmentSort.UpgradeLevel => "UPGRADE",
        _ => "POWER"
    };
    static string Pretty(EquipmentPerkType type) => type switch { EquipmentPerkType.CritChance => "Crit Chance", EquipmentPerkType.RecoverySpeed => "Recovery", _ => type.ToString() };
    static string PrettyValue(EquipmentModifier modifier) => modifier.type is EquipmentPerkType.CritChance or EquipmentPerkType.RecoverySpeed ? (modifier.value * (modifier.type == EquipmentPerkType.RecoverySpeed ? 100 : 1)).ToString("0.#") + "%" : modifier.value.ToString("0.#");
    static string Signed(float value) => value > 0 ? "+" + value.ToString("0.#") : value.ToString("0.#");
    static Color RarityColor(EquipmentRarity rarity) => rarity switch { EquipmentRarity.Epic => new Color(.54f, .22f, .8f), EquipmentRarity.Rare => new Color(.14f, .48f, .95f), EquipmentRarity.Uncommon => new Color(.18f, .68f, .35f), _ => new Color(.43f, .43f, .45f) };
    static float Scale() => Mathf.Clamp(Mathf.Min(Screen.width / 540f, Screen.height / 960f), .45f, 1.5f);
    static Rect Safe(float scale) { Rect safe = Screen.safeArea; return new Rect(safe.xMin / scale, (Screen.height - safe.yMax) / scale, safe.width / scale, safe.height / scale); }
}
