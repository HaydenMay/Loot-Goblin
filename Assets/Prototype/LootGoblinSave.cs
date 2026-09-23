using System;
using UnityEngine;

// The intentionally small persistent contract for Retention V0. Add future fields only when
// their owning feature exists; run-local state such as carried gold does not belong here.
[Serializable]
public sealed class LootGoblinSaveData
{
    public int version = LootGoblinSave.CurrentVersion;
    public int bankedGold;
    // Equipment instances are plain data, never Unity object references, so rolls remain
    // portable across PlayerPrefs/WebGL and reuse the same authored model per definition.
    public System.Collections.Generic.List<OwnedEquipmentItem> ownedEquipment = new();
}

public static class LootGoblinSave
{
    const string SaveKey = "LootGoblin.SaveData";
    public const int CurrentVersion = 2;

    public static LootGoblinSaveData Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return NewSave();

        try
        {
            LootGoblinSaveData data = JsonUtility.FromJson<LootGoblinSaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null) throw new ArgumentException("Save JSON produced no data.");

            // Version 0 is the safe default for a missing version field in an early save.
            if (data.version <= 0) data.version = CurrentVersion;
            data.bankedGold = Mathf.Max(0, data.bankedGold);
            data.ownedEquipment ??= new System.Collections.Generic.List<OwnedEquipmentItem>();
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Loot Goblin save data could not be read; starting with 0 banked gold. {exception.Message}");
            return NewSave();
        }
    }

    public static void Save(LootGoblinSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        data.version = CurrentVersion;
        data.bankedGold = Mathf.Max(0, data.bankedGold);
        data.ownedEquipment ??= new System.Collections.Generic.List<OwnedEquipmentItem>();
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        // PlayerPrefs is Unity's platform-backed preference store, including WebGL's browser storage.
        PlayerPrefs.Save();
    }

    static LootGoblinSaveData NewSave() => new();
}
