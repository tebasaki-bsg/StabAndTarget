using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using Modding;
using Modding.Common;

namespace StaTSpace
{
    /// <summary>
    /// IFFブロックの辞書と、ロックオンを行うクラス。
    /// IFFDict: 全てのIFFが入る辞書
    /// IFFTeamListDict: チームと、そのチームが敵となるIFFのIDをまとめたリストを紐づける辞書
    /// PrimaryLockedList: 一次ロック（ロック可能領域内侵入で即完了）済のIFFのリスト
    /// SecondaryLockingTimerDict: 二次ロックまでの残り時間
    /// SecondaryLockedList: 二次ロック済のIFFのリスト
    /// PlayerLockedListDict: 各プレイヤーと、そのTargetingListを紐づける辞書
    /// </summary>
    public class StaTTargetController : MonoBehaviour
    {
        /// <summary>
        /// IFFの取り扱いについて
        /// ①IFFにSessionIDを与え、IFF全体をまとめたIFFDictに登録
        /// ②チームごとに敵となるIFFのIDをまとめたIFFTeamListDictに登録
        /// ③各プレイヤーのStaTTargetControllerが、IFFTeamListDict内の自チームのListから、ロックオン領域内にあるもののSessionIDを取得し、PrimaryLockedListに入れる（一次ロック）
        /// ④SecondaryLockingDict[SessionID]が0以下になったら二次ロックが完了、SecondaryLockedListに入れる
        /// ④-2: ロック準備中に領域を外れたらPrimaryLockedList, SecondaryLockedList, SecondaryLockingDictから削除
        /// ④-3: 一次ロック時, 二次ロック完了時, ロック解除時にそのIFFに通知が飛ぶ
        /// </summary>

        public static StaTTargetController Instance { get; private set; }   //シングルトン

        //各辞書・リストの宣言（意味は上記を参照）
        public static Dictionary<int, IFFEntry> IFFDict = new Dictionary<int, IFFEntry>();
        public static Dictionary<MPTeam, List<int>> IFFTeamListDict = new Dictionary<MPTeam, List<int>>()
        {
            {MPTeam.None, new List<int>() },
            {MPTeam.Red, new List<int>() },
            {MPTeam.Green, new List<int>() },
            {MPTeam.Orange, new List<int>() },
            {MPTeam.Blue, new List<int>() }
        };
        public static List<int> PrimaryLockedList = new List<int>();
        public static Dictionary<int, float> SecondaryLockingTimerDict = new Dictionary<int, float>();
        public static List<int> SecondaryLockedList = new List<int>();

        public static bool init = false;    //シミュ開始時にfalseの場合はSimulationStartInit()を走らせる関数
        public static int CurrentAimID = 10000; //自分が現在照準中の敵のID
        public static int CurrentOrder = 0; //自分がロックオン中の敵のうち何番目を照準しているか
        public static bool LockingSomething = false;    //何かしらをロックオンしているか（一次ロック含む）
        public static int MyPlayerID = 0;   //自分のNetworkID,シミュ開始時に変更
        public static MPTeam MyTeam = MPTeam.None;    //シミュ開始時にコアブロックから指定

        public static Vector3 CorePosition; //コアブロックの座標
        public static Vector3 CamForward;   //プレイヤーのカメラ方向
        public static bool TargetChangePressed = false;  //ターゲット切替えキーが押されたかの確認, キーはコアブロックにある

        private List<int> LastSentPrimary = new List<int>();    //最後にホストに送信したリスト
        private List<int> LastSentSecondary = new List<int>();    //最後にホストに送信したリスト
        private Camera MainCamera = Camera.main;

        public void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 初期化。自分のNetworkIDを取得し、IFFTeamListDictを初期化。
        /// シミュ開始時にコアブロックから呼ぶ。
        /// </summary>
        public static void SimulationStartInit()
        {
            var localPlayer = Player.GetLocalPlayer();
            MyPlayerID = localPlayer.NetworkId;

            init = true;
        }

        /// <summary>
        /// ロックオン状態の確認→ターゲット切替えが押されていれば行う→クライアントがホストに各種情報を送信
        /// </summary>
        public void FixedUpdate()
        {
            if(StatMaster.PlayMode == BesiegePlayMode.BuildMode || StatMaster.PlayMode == BesiegePlayMode.Spectator)
            {
                return;
            }

            if(!init)
            {
                return;
            }

            CamForward = MainCamera.transform.forward;

            //各IFFのロックオン状態を更新
            UpdateLock(MyTeam, CorePosition, Time.fixedDeltaTime);

            //ターゲット切替えボタンが押されていればターゲットを切替え
            if (TargetChangePressed)
            {
                OnTargetChangePressed();
                TargetChangePressed = false;
            }
            
            //マルチじゃない or ホスト or ローカルシミュ の場合は送信の必要が無いためここで切り上げ
            if (!StatMaster.isMP || StatMaster.isHosting || StatMaster.isLocalSim)
            {
                return;
            }

            //ロック済リストが更新されていればホストに送信
            SendLockListsIfChanged();
            
            //何もロックして無ければカメラ方向、ロックしていれば照準中の対象をホストに送信
            if (!LockingSomething)
            {
                ModNetworking.SendToHost(StaTMessageController.CurrentCameraMessageType.CreateMessage(MyPlayerID, CamForward));
            }
            else
            {
                ModNetworking.SendToHost(StaTMessageController.CurrentAimingMessageType.CreateMessage(MyPlayerID, CurrentAimID, CurrentAimState()));
            }
        }

        /// <summary>
        /// ロック可能範囲内にいるか判定する関数
        /// </summary>
        public bool InRegion(int id, Vector3 corePos,
              out float sqrDistance)
        {
            //IFFDictからBBを取得
            BlockBehaviour iffBB = IFFDict[id].BlockBehaviour;

            //自機との距離の二乗を計算
            Vector3 DistanceVector = iffBB.transform.position - corePos;
            sqrDistance = Vector3.Dot(DistanceVector, DistanceVector);

            // 1. 距離(最も安価)
            if (sqrDistance > StaTTargetConfig.maxRange)
            {
                return false;
            }

            // 2. 前方判定（WorldToScreenPointのz座標が0以上）
            if (MainCamera.WorldToScreenPoint(iffBB.transform.position).z < 0)
            {
                return false;
            }

            // 3. ビューポート矩形（0.5-幅 ≦ ビューポート座標x ≦ 0.5+幅 かつ 0.5-高さ ≦ ビューポート座標y ≦ 0.5+高さ の時のみtrue）
            Vector3 viewPortVector = MainCamera.WorldToViewportPoint(iffBB.transform.position);
            return viewPortVector.x >= 0.5f - StaTTargetConfig.LockAreaWidth && viewPortVector.x <= 0.5f + StaTTargetConfig.LockAreaWidth
                && viewPortVector.y >= 0.5f - StaTTargetConfig.LockAreaHeight && viewPortVector.y <= 0.5f + StaTTargetConfig.LockAreaHeight;
        }

        /// <summary>
        /// 各IFFのロックオン状態を更新する関数
        /// </summary>
        public void UpdateLock(MPTeam team, Vector3 corePos, float dt)
        {
            if(!IFFTeamListDict.ContainsKey(team))
            {
                return;
            }

            foreach (int id in IFFTeamListDict[team])
            {
                IFFEntry iffEntry;

                if (!IFFDict.TryGetValue(id, out iffEntry) || iffEntry == null)
                {
                    PrimaryLockedList.Remove(id);
                    SecondaryLockedList.Remove(id);
                    SecondaryLockingTimerDict.Remove(id);
                    OnLockRemoved(id); // 6節: CurrentAimIdの再選択

                    continue;
                }

                //距離とカメラ方向との内積, ロックオン領域内か判定する関数でも使う
                float sqrDistance;
                //ロック可能範囲内か
                bool inRegion = InRegion(id, corePos, out sqrDistance);

                //各リスト内にいるか
                bool wasPrimary = PrimaryLockedList.Contains(id);
                bool wasSecondary = SecondaryLockedList.Contains(id);

                //ロック可能領域内にいる時
                if (inRegion)
                {
                    //一次, 二次ロック済どちらにもいない場合
                    if (!wasPrimary && !wasSecondary)
                    {
                        PrimaryLockedList.Add(id); // 一次ロック即時完了
                        float lockTime = Mathf.Clamp(StaTTargetConfig.LockTimeProportional * sqrDistance + StaTTargetConfig.LockTimeConstant, StaTTargetConfig.minLockTime, StaTTargetConfig.maxLockTime);
                        SecondaryLockingTimerDict[id] = lockTime;

                        Mod.Log("SqrDistance is " + sqrDistance + ", LockTime is " + lockTime.ToString());

                        //そのIFFにロック状態を通知
                        iffEntry.LockState = StaTLockState.Primary;
                        iffEntry.IFFBehaviour.lockTime = lockTime;
                        iffEntry.IFFBehaviour.LockStateChanged(StaTLockState.Primary);

                        AutoReselect();
                        
                    }
                    //二次ロック済にいる場合
                    else if (wasSecondary)
                    {
                        continue;
                    }
                    //一次ロック済にいて二次ロック済にいない場合
                    else if (wasPrimary && !wasSecondary)
                    {
                        float remain;
                        if (SecondaryLockingTimerDict.TryGetValue(id, out remain))
                        {
                            remain -= dt;
                            if (remain <= 0f)
                            {
                                SecondaryLockingTimerDict.Remove(id);
                                PrimaryLockedList.Remove(id);
                                SecondaryLockedList.Add(id); // 二次ロック完了

                                iffEntry.LockState = StaTLockState.Secondary;
                                iffEntry.IFFBehaviour.LockStateChanged(StaTLockState.Secondary);

                                AutoReselect();

                            }
                            else
                            {
                                SecondaryLockingTimerDict[id] = remain;
                            }
                        }
                    }
                }
                //ロック可能範囲外の場合はロックオン済およびロックオン中から外す
                else if (wasPrimary || wasSecondary)
                {
                    iffEntry.LockState = StaTLockState.None;
                    iffEntry.IFFBehaviour.LockStateChanged(StaTLockState.None);

                    //一次ロックの場合は一次ロックと二次ロック準備中のリストから外す
                    PrimaryLockedList.Remove(id);
                    SecondaryLockingTimerDict.Remove(id);
                    SecondaryLockedList.Remove(id);
                    OnLockRemoved(id); // CurrentAimIdの再選択
                }
            }
        }



        /// <summary>
        /// ターゲット順のリスト。二次ロック済リストの後ろに一次ロック済リストを加えたもの。
        /// </summary>
        private List<int> CombinedOrder()
        {
            return SecondaryLockedList.Concat(PrimaryLockedList).ToList();
        }

        /// <summary>
        /// 現在照準中の敵が何次ロックか
        /// </summary>
        private StaTLockState CurrentAimState()
        {
            //何もロックしてなければ0
            if(!LockingSomething)
            {
                return 0;
            }

            //二次ロック済リストにあれば二次ロック、なければ一次ロック
            return SecondaryLockedList.Contains(CurrentAimID) ? StaTLockState.Secondary : StaTLockState.Primary;
        }

        /// <summary>
        /// ロック中の何かがロック可能領域外に出た時に呼ばれ、現在照準中の敵であれば照準対象を自動で切替えさせる関数
        /// </summary>
        public void OnLockRemoved(int id)
        {
            if (CurrentAimID == id) AutoReselect();
        }

        /// <summary>
        /// 照準対象を自動で０番に切替える関数
        /// </summary>
        public void AutoReselect()
        {
            var order = CombinedOrder();

            //変更不要ならここで抜ける（状態を一切いじらない）
            if (order.Count > 0 && CurrentAimID == order[0])
            {
                return;
            }

            //古い方のIFFがまだ存在するなら被照準状態を解除（死んでいて存在しない場合は何もしない）
            IFFEntry oldIFFEntry;
            if (IFFDict.TryGetValue(CurrentAimID, out oldIFFEntry))
            {
                oldIFFEntry.CurrentAiming = false;
                oldIFFEntry.IFFBehaviour.CurrentAimingChanged(false);
            }

            if (order.Count > 0)
            {
                CurrentAimID = order[0];    //二次→一次
                CurrentOrder = 0;
                LockingSomething = true;

                //新しい方のIFFを被照準状態に
                IFFEntry newIFFEntry = IFFDict[CurrentAimID];
                newIFFEntry.CurrentAiming = true;
                newIFFEntry.IFFBehaviour.CurrentAimingChanged(true);
            }
            else
            {
                CurrentAimID = 10000;
                LockingSomething = false;
                CurrentOrder = 0;
            }
        }

        /// <summary>
        /// ターゲット切替えキーが押されたら切替えを行う関数
        /// </summary>
        public void OnTargetChangePressed()
        {
            var order = CombinedOrder();

            if(order.Count == 0)
            {
                return; 
            }
            else if(order.Count == 1)
            {
                return;
            }
            else
            {
                //古い方のIFFの被照準状態を解除
                IFFEntry oldIFFEntry = IFFDict[CurrentAimID];
                oldIFFEntry.CurrentAiming = false;
                oldIFFEntry.IFFBehaviour.CurrentAimingChanged(false);

                //オーダーを1進めて、オーダー表内の個数で割った値を新たにオーダーとする。例：3個ロックオン中、オーダーが2（3番目）の場合 → オーダー = (2+1)%3 = 0
                CurrentOrder = (CurrentOrder + 1) % order.Count;
                CurrentAimID = order[CurrentOrder];

                //新しい方のIFFを被照準状態に
                IFFEntry newIFFEntry = IFFDict[CurrentAimID];
                newIFFEntry.CurrentAiming = true;
                newIFFEntry.IFFBehaviour.CurrentAimingChanged(true);
            }
            
        }

        /// <summary>
        /// ロック済リストが変わっていたらホストに送信する関数
        /// </summary>
        public void SendLockListsIfChanged()
        {
            if (!PrimaryLockedList.SequenceEqual(LastSentPrimary))
            {
                ModNetworking.SendToHost(StaTMessageController.PrimaryChangeMessageType.CreateMessage(MyPlayerID, PrimaryLockedList.ToArray()));
                LastSentPrimary = new List<int>(PrimaryLockedList);
            }
            if (!SecondaryLockedList.SequenceEqual(LastSentSecondary))
            {
                ModNetworking.SendToHost(StaTMessageController.SecondaryChangeMessageType.CreateMessage(MyPlayerID, SecondaryLockedList.ToArray()));
                LastSentSecondary = new List<int>(SecondaryLockedList);
            }
        }
    }

    /// <summary>
    /// 何次ロックか
    /// </summary>
    public enum StaTLockState
    {
        None = 0,
        Primary = 1,
        Secondary = 2
    }
}
