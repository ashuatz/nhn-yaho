using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 아웃게임 창고. 가치 합계가 아니라 안정 ID 기반 아이템 목록을 저장한다
    /// (교차 검증 반영 - 조건형 아이템, 강화 등 아웃게임 확장의 데이터 기반).
    /// 저장은 임시파일 작성 후 교체(원자적), 손상 파일은 .corrupt로 격리 후 빈 창고로 복구.
    /// 순수 로직은 EditMode 테스트 대상 - 파일 경로를 주입받는다.
    /// </summary>
    [Serializable]
    public sealed class PlayerStash
    {
        [Serializable]
        public sealed class StashEntry
        {
            public string itemId;
            public int count;
        }

        [Serializable]
        sealed class StashFile
        {
            public int version = CurrentVersion;
            public List<StashEntry> entries = new List<StashEntry>();
        }

        public const int CurrentVersion = 1;

        StashFile data = new StashFile();

        public IReadOnlyList<StashEntry> Entries
        {
            get { return data.entries; }
        }

        public static string DefaultPath
        {
            get { return Path.Combine(Application.persistentDataPath, "player_stash.json"); }
        }

        public void AddItem(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0)
                return;

            foreach (StashEntry entry in data.entries)
            {
                if (entry.itemId != itemId)
                    continue;

                entry.count += count;
                return;
            }

            data.entries.Add(new StashEntry { itemId = itemId, count = count });
        }

        public int CountOf(string itemId)
        {
            foreach (StashEntry entry in data.entries)
            {
                if (entry.itemId == itemId)
                    return entry.count;
            }

            return 0;
        }

        // -- 직렬화 ----------------------------------------------------------

        public string ToJson()
        {
            return JsonUtility.ToJson(data, true);
        }

        public static PlayerStash FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new PlayerStash();

            StashFile parsed = JsonUtility.FromJson<StashFile>(json);

            if (parsed == null || parsed.entries == null)
                throw new FormatException("Stash JSON malformed.");

            PlayerStash stash = new PlayerStash();
            stash.data = parsed;
            return stash;
        }

        // -- 파일 IO ---------------------------------------------------------

        public static PlayerStash LoadFrom(string path)
        {
            if (!File.Exists(path))
                return new PlayerStash();

            try
            {
                string json = File.ReadAllText(path);
                return FromJson(json);
            }
            catch (Exception e)
            {
                // 손상 파일은 격리하고 빈 창고로 복구 - 런 진행을 막지 않는다
                UnityEngine.Debug.LogWarning($"[Stash] Corrupt stash file, quarantined: {e.Message}");
                QuarantineCorruptFile(path);
                return new PlayerStash();
            }
        }

        public void SaveTo(string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // 원자적 저장: 임시파일 완성 후 교체. 저장 중 크래시에도 원본 보존
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, ToJson());

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
                return;
            }

            File.Move(tempPath, path);
        }

        static void QuarantineCorruptFile(string path)
        {
            try
            {
                string quarantinePath = path + ".corrupt";

                if (File.Exists(quarantinePath))
                    File.Delete(quarantinePath);

                File.Move(path, quarantinePath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Stash] Failed to quarantine corrupt file: {e.Message}");
            }
        }
    }
}
