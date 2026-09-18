using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Modding;
using Modding.Modules;
using Modding.Serialization;
using Modding.Blocks;
using Modding.Common;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using UnityEngine.UI;
using Localisation;

namespace StaTSpace
{
    //XML上での名前
    [XmlRoot("StaTIFFBlockModule")]
    [Reloadable]

    public class StaTIFFBlockModule : BlockModule
    {
        [XmlElement("IsEnemyToggle")]
        [RequireToValidate]
        public MToggleReference IsEnemyToggle;

        [XmlElement("Icon")]
        [RequireToValidate]
        public ResourceReference icon;

        [XmlElement("LockIcon")]
        [RequireToValidate]
        public ResourceReference lockIcon;
    }

    public class StaTIFFBlockModuleBehaviour : BlockModuleBehaviour<StaTIFFBlockModule>
    {
        /*
         * IFFブロックが持つ情報
         *      MyName:     機体名
         *      isEnemy:    敵であるか（ONにすると同じプレイヤー・チームでも照準可能に）
         *      Team:       チーム（自前で取得）
         *      Alive:      生存中か
         *      UseHealth:  体力バーを使うか
         *      Health:     現在の体力
         *      LockState:  ロックオンの状態
         *      LockTime:   二次ロック所要時間
         *      CurrentAiming:  プレイヤーに現在照準されているか
         */

        public MToggle IsEnemyToggle;
        public bool isEnemy = false;

        public MText MyNameText;
        internal string myName;

        internal int sessionID = 10000;
        internal MPTeam team;
        internal bool alive;
        internal bool useHealth = false;
        internal float health = 0f;
        internal StaTLockState lockState = StaTLockState.None;
        internal float lockTime;
        internal bool currentAiming = false;

        private bool UIColorInit = false;
        public GameObject IFFIconObject;    //UIの親も兼ねる
        public GameObject NameIconObject;
        public GameObject LockIconObject;
        public Sprite IFFIcon;
        public Sprite LockIcon;

        public RectTransform IFFRect;
        public Image IFFImage;
        public Image LockImage;
        public Text NameUIText;
        public Font Arial;

        public Camera MainCamera = Camera.main;
        public bool LastVisible = false;
        public Vector3 PositionVector;

        public static Dictionary<MPTeam, Color> TeamColors = new Dictionary<MPTeam, Color>
        {
            {MPTeam.None,   Color.white},
            {MPTeam.Red,    Color.red},
            {MPTeam.Green,  new Color(0.2f, 0.8f, 0.2f)},
            {MPTeam.Orange, new Color(1.0f, 0.6f, 0.1f)},
            {MPTeam.Blue,   new Color(0.2f, 0.6f, 1.0f)},
        };

        public static Dictionary<StaTLockState, Color> LockStateColors = new Dictionary<StaTLockState, Color>
        {
            {StaTLockState.None, Color.white},
            {StaTLockState.Primary, new Color(0.2f, 0.8f, 0.2f)},
            {StaTLockState.Secondary, Color.red}
        };

        public static bool UseUI = true;
        public static Color AlertColor = Color.red;
        public static Color FriendColor = new Color(0.2f, 0.6f, 1.0f);

        public string MyName
        {
            get
            {
                return myName;
            }
        }

        public int SessionID
        {
            get
            {
                return sessionID;
            }
        }

        public MPTeam Team
        {
            get
            {
                return team;
            }
        }

        public bool Alive
        {
            get
            {
                return alive;
            }
        }
        public bool UseHealth
        {
            get
            {
                return useHealth;
            }
        }
        public float Health     //必ず最大値で割ることで 0≦Health≦1 とすること。
        {
            get
            {
                return health;
            }
        }

        public StaTLockState LockState
        {
            get
            {
                return lockState;
            }
        }

        public float LockTime
        {
            get
            {
                return lockTime;
            }
        }

        public bool CurrentAiming
        {
            get
            {
                return currentAiming;
            }
        }

        public override void SafeAwake()
        {
            base.SafeAwake();

            //設定欄にエフェクトの名前を追加
            MyNameText = BlockBehaviour.AddText(Mod.isJapanese ? "機体名" : "Machine Name", "machine-name", "UNKNOWN");
            MyNameText.DisplayInMapper = true;

            //建築中⇒IFFの辞書をクリア（シミュ開始時に綺麗な状態とするため）、アイコン関係を初期化
            if(BlockBehaviour.isBuildBlock)
            {
                //フォント読み込み
                Arial = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;

                //画像読み込み
                ModTexture modTexture = (ModTexture)GetResource(Module.icon);   //IFFのアイコン
                modTexture.Texture.wrapMode = TextureWrapMode.Clamp;    //端は区切る
                IFFIcon = Sprite.Create(modTexture.Texture, new Rect(0, 0, modTexture.Texture.width, modTexture.Texture.height), Vector2.zero);   //スプライト生成

                modTexture = (ModTexture)GetResource(Module.lockIcon);   //ロック時のアイコン
                modTexture.Texture.wrapMode = TextureWrapMode.Clamp;    //端は区切る
                LockIcon = Sprite.Create(modTexture.Texture, new Rect(0, 0, modTexture.Texture.width, modTexture.Texture.height), Vector2.zero);   //スプライト生成

                InitUI();
            }
        }

        ///<summary>
        ///シミュ開始時、ID, ブロックの持ち主のID, BlockBehaviourを揃って登録
        /// </summary>
        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            IsEnemyToggle = GetToggle(Module.IsEnemyToggle);
            isEnemy = IsEnemyToggle.IsActive;

            myName = MyNameText.Value;
            NameUIText.text = myName;

            //自分のチームを取得。バレンなどの場合はチームが無いためNone
            if(!StatMaster.isMP)
            {
                team = MPTeam.None;
            }
            else
            {
                Player Player = Player.From(BlockBehaviour.ParentMachine.PlayerID);
                team = Player.Team;
            }

            
            NameUIText.color = TeamColors[team];

            //味方であれば味方用のカラーに
            if(!isEnemy && team == StaTTargetController.MyTeam)
            {
                IFFImage.color = FriendColor;
                LockImage.color = FriendColor;
            }

            LockIconObject.SetActive(false);

            //ホストならID登録を行う
            if (StatMaster.isHosting || !StatMaster.isMP || StatMaster.isLocalSim)
            {
                //ID登録命令
                sessionID = StatTIFFIDRegister.RegisterIFF(BlockBehaviour, this);
            }
        }

        //リスポorシミュ停止時
        public override void OnSimulateStop()
        {
            base.OnSimulateStop();

            //リスポ・シミュ停止両方でUIを消す
            IFFIconObject.SetActive(false);
        }

        //OnGUIほど高頻度で回したくないのでこっちで
        public override void SimulateUpdateAlways()
        {
            if(!StaTStartingBlockScript.ActivateUI)
            {
                IFFIconObject.SetActive(false);
                return;
            }

            if(!UIColorInit)
            {
                //味方であれば味方用のカラーに
                if (!isEnemy && team == StaTTargetController.MyTeam)
                {
                    IFFImage.color = FriendColor;
                    LockImage.color = FriendColor;
                }
                else
                {
                    ColorChange(lockState);
                }

                UIColorInit = true;
            }

            //IFFのスクリーン上の座標（スケーリング前）を取得
            PositionVector = MainCamera.WorldToScreenPoint(transform.position);
            Vector2 PositionVector2D = new Vector2(PositionVector.x, PositionVector.y);

            //実際にUIに渡すスクリーン上の座標（スケーリング後）を作る
            Vector2 rectPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)Mod.ModCanvas.transform,
                PositionVector2D,
                null,
                out rectPoint
                );
            IFFRect.anchoredPosition = rectPoint;

            //画面手前にいるかと最後に映っていたかが一致しない場合はUIが映るかを変更
            if (PositionVector.z > 0 != LastVisible)
            {
                LastVisible = !LastVisible;

                IFFIconObject.SetActive(LastVisible);
            }
        }

        public void Dead()
        {
            alive = false;
            StatTIFFIDRegister.RemoveMe(sessionID);
        }

        public void InitUI()
        {
            //終わってたらスルー
            if(IFFIconObject != null)
            {
                return;
            }
            ///<summary>
            ///IFFIconObjectの初期化
            /// </summary>
            IFFIconObject = new GameObject("IFFIcon", typeof(RectTransform), typeof(Image));
            IFFIconObject.transform.SetParent(Mod.StaTMod.transform);
            IFFIconObject.SetActive(false);

            IFFRect = IFFIconObject.GetComponent<RectTransform>();
            IFFRect.sizeDelta = new Vector2(300f, 300f);
            IFFRect.anchorMin = new Vector2(0.5f, 0.5f);
            IFFRect.anchorMax = new Vector2(0.5f, 0.5f);
            IFFRect.localScale = Vector3.one * 0.4f;

            IFFImage = IFFIconObject.GetComponent<Image>();
            IFFImage.sprite = IFFIcon;
            IFFImage.color = LockStateColors[StaTLockState.None];

            ///<summary>
            ///LockIconObjectの初期化
            /// </summary>
            LockIconObject = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            LockIconObject.transform.SetParent(IFFIconObject.transform);
            LockIconObject.SetActive(false);

            RectTransform LockRect = LockIconObject.GetComponent<RectTransform>();
            LockRect.sizeDelta = new Vector2(300f, 300f);
            LockRect.anchorMin = new Vector2(0.5f, 0.5f);
            LockRect.anchorMax = new Vector2(0.5f, 0.5f);
            LockRect.localScale = Vector3.one;
            
            LockImage = LockIconObject.GetComponent<Image>();
            LockImage.sprite = LockIcon;
            LockImage.color = LockStateColors[StaTLockState.None];

            ///<summary>
            ///NameIconObjectの初期化
            /// </summary>
            NameIconObject = new GameObject("NameIcon", typeof(RectTransform), typeof(Text));
            NameIconObject.transform.SetParent(IFFIconObject.transform);

            RectTransform NameRect = NameIconObject.GetComponent<RectTransform>();
            NameRect.sizeDelta = new Vector2(200f, 100f);
            NameRect.anchorMin = new Vector2(0.5f, 0.5f);
            NameRect.anchorMax = new Vector2(0.5f, 0.5f);
            NameRect.anchoredPosition = new Vector2(65, 50);
            NameRect.localScale = Vector3.one;

            NameUIText = NameIconObject.GetComponent<Text>();
            NameUIText.text = "Nothing";
            NameUIText.font = Arial;
            NameUIText.fontSize = 35;
            NameUIText.color = Color.white;
            NameUIText.alignment = TextAnchor.MiddleRight;  //右揃え、左端が中心
        }

        public void LockStateChanged(StaTLockState value)
        {
            ColorChange(value);

            lockState = value;
        }

        public void CurrentAimingChanged(bool value)
        {
            currentAiming = value;

            LockIconObject.SetActive(value);
        }

        public void ColorChange(StaTLockState sendedLockState)
        {
            lockState = sendedLockState;

            IFFImage.color = LockStateColors[lockState];
            LockImage.color = LockStateColors[lockState];
        }

        public void ColorChangeAlert()
        {
            IFFImage.color = AlertColor;
            LockImage.color = AlertColor;
        }
    }
}
