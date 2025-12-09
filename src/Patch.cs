using System;
using System.Linq;
using HarmonyLib;

namespace SelectShrineBoss
{
    [HarmonyPatch]
    internal class Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TraitShrine), nameof(TraitShrine._OnUse))]
        private static bool Prefix(TraitShrine __instance)
        {
            if (__instance.Shrine.id != "strife")
                // 争いの祠以外の場合は既存処理を実行
                return true;

            // 祠の座標を取得
            var point = __instance.owner.ExistsOnMap ? __instance.owner.pos : EClass.pc.pos;

            // バイオーム情報を祠座標から取得
            var biome = point.cell.biome;

            // ボス用のスポーン設定を生成
            var spawnSettings = SpawnSetting.Boss(__instance.owner.LV);

            // 危険度取得
            var dangerLv = spawnSettings.dangerLv == -1 ? EClass._zone.DangerLv : spawnSettings.dangerLv;

            // 生成レベル取得
            var lv = spawnSettings.filterLv == -1 ? dangerLv : spawnSettings.filterLv;

            // ゾーンがすくつスケーリングの対象である場合
            if (EClass._zone.ScaleType == ZoneScaleType.Void)
            {
                // 生成レベルに危険度を反映
                lv = ((dangerLv - 1) % 50 + 5) * 150 / 100;

                // 既存処理では乱数生成を使用しているが実装意図がよくわからないのでコメントアウト
                // if (lv >= 20 && EClass.rnd(100) < lv)
                //     lv = dangerLv;
            }

            // 生成可能モンスターリストを取得
            var spawnList = CreateSpawnList(spawnSettings, biome);

            // レベルでリストをフィルタ
            var filteredList = spawnList.Filter(lv);

            // Lvで降順ソート
            var sortedRows = filteredList.rows
                .OrderByDescending(row => row.LV)
                .ToList();

            // UI表示
            EClass.ui.AddLayer<LayerList>()
                .SetSize(400)
                .SetList(sortedRows,
                    (row) => $"LV.{row.LV} {row.GetName()}",
                    (index, _) =>
                    {
                        // リスト選択時処理を設定
                        SpawnEnemies(point, __instance, sortedRows[index]);
                    })
                .SetHeader($"Select Boss (Lv.{lv})");

            // 既存処理をスキップ
            return false;
        }

        // 既存処理と同じメソッドを使用して敵キャラを生成する
        private static void SpawnEnemies(Point point, TraitShrine shrine, CardRow bossRow)
        {
            var count = 3 + EClass.rnd(2);

            // ボスを生成
            EClass._zone
                .SpawnMob(point.GetNearestPoint(allowChara: false),
                    SpawnSetting.Boss(bossRow.id, fixedLv: shrine.owner.LV))?.PlayEffect("teleport");

            // モブを生成
            for (var i = 1; i < count; i++)
            {
                EClass._zone
                    .SpawnMob(point.GetNearestPoint(allowChara: false),
                        SpawnSetting.DefenseEnemy(shrine.owner.LV))?.PlayEffect("teleport");
            }
        }

        // Zone.csのSpawnMobメソッド内のspawnList変数作成処理を移植
        private static SpawnList CreateSpawnList(SpawnSetting setting, BiomeProfile? biome)
        {
            SpawnList spawnList;
            if (setting.idSpawnList != null)
            {
                spawnList = SpawnList.Get(setting.idSpawnList);
            }
            else
            {
                switch (EClass._zone)
                {
                    case Zone_DungeonYeek _ when EClass.rnd(5) != 0:
                        spawnList = SpawnListChara.Get("dungeon_yeek",
                            (Func<SourceChara.Row, bool>)(r => r.race == "yeek" && r.quality == 0));
                        break;
                    case Zone_DungeonDragon _ when EClass.rnd(5) != 0:
                        spawnList = SpawnListChara.Get("dungeon_dragon",
                            (Func<SourceChara.Row, bool>)(r =>
                                (r.race == "dragon" || r.race == "drake" || r.race == "wyvern" ||
                                 r.race == "lizardman" || r.race == "dinosaur") && r.quality == 0));
                        break;
                    case Zone_DungeonMino _ when EClass.rnd(5) != 0:
                        spawnList = SpawnListChara.Get("dungeon_mino",
                            (Func<SourceChara.Row, bool>)(r => r.race == "minotaur" && r.quality == 0));
                        break;
                    default:
                        if (setting.hostility == SpawnHostility.Neutral || setting.hostility != SpawnHostility.Enemy &&
                            Rand.Range(0.0f, 1f) < (double)EClass._zone.ChanceSpawnNeutral)
                        {
                            spawnList = SpawnList.Get("c_neutral");
                            break;
                        }

                        if (biome?.spawn.chara.Count > 0)
                        {
                            spawnList = SpawnList.Get(biome.spawn.GetRandomCharaId());
                            break;
                        }

                        spawnList = SpawnList.Get(biome?.name, "chara", new CharaFilter()
                        {
                            ShouldPass = (Func<SourceChara.Row, bool>)(s =>
                            {
                                if (s.hostility != "")
                                    return false;
                                return s.biome == biome?.name || s.biome.IsEmpty();
                            })
                        });
                        break;
                }
            }
            return spawnList;
        }
    }
}