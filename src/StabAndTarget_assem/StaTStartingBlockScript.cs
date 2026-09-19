using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Modding;
using Modding.Blocks;
using Modding.Common;
using Modding.Modules;


namespace StaTSpace
{
    /// <summary>
    /// コアブロックに追加するスクリプト
    /// </summary>
    public class StaTStartingBlockScript : BlockScript
    {
        public static StaTStartingBlockScript Instance { get; private set; }   //シングルトン, ホストのコアブロに付与

        public MKey ActivateUIKey;
        public MKey TargetChangeKey;

        public MSlider VolumeSlider;

        public MToggle AppearUIConfigToggle;

        public MColourSlider BackgroundColorSlider;
        public MColourSlider NoneColorSlider;
        public MColourSlider PrimaryColorSlider;
        public MColourSlider SecondaryColorSlider;

        public Rigidbody rigidbody;
        public BlockBehaviour blockBehaviour;

        public bool localIsOwner = true;

        public static bool ActivateUI = false;
        public GameObject LockAreaObject;
        public Texture2D LockAreaTexture;
        public Sprite LockAreaIcon;
        public Image LockAreaImage;

        public Camera MainCamera = Camera.main;

        //シミュ開始時ならば各種情報を登録
        public void Awake()
        {
            blockBehaviour = GetComponent<BlockBehaviour>();

            //UI起動キー、ターゲット切替えキーを追加
            ActivateUIKey = blockBehaviour.AddKey(Mod.isJapanese ? "StaT: UI起動" : "Activate UI", "stat-activate-ui", KeyCode.P);
            TargetChangeKey = blockBehaviour.AddKey(Mod.isJapanese ? "ターゲット切替え":"Change Target", "target-change", KeyCode.B);
            VolumeSlider = blockBehaviour.AddSlider(Mod.isJapanese ? "StaT: ロック音量" : "StaT: Sound", "stat-sound-volume", 0.2f, 0.01f, 1f);

            AppearUIConfigToggle = blockBehaviour.AddToggle(Mod.isJapanese ? "StaT: UIの色を変更" : "StaT: UI color", "pd-config", false);
            AppearUIConfigToggle.DisplayInMapper = true;
            AppearUIConfigToggle.Toggled += AppearUIConfig;

            BackgroundColorSlider = blockBehaviour.AddColourSlider(Mod.isJapanese ? "背景色" : "Background Color", "stat-bg-color", StaTUIInfo.LockStateColors[StaTLockState.None], true);
            BackgroundColorSlider.DisplayInMapper = false;
            BackgroundColorSlider.ValueChanged += ChangeBackgroundColor;

            NoneColorSlider = blockBehaviour.AddColourSlider(Mod.isJapanese ? "通常色" : "Normal Color", "stat-none-color", StaTUIInfo.LockStateColors[StaTLockState.None], true);
            NoneColorSlider.DisplayInMapper = false;
            NoneColorSlider.ValueChanged += ChangeNoneColor;

            PrimaryColorSlider = blockBehaviour.AddColourSlider(Mod.isJapanese ? "一次ロック" : "PrimaryLock Color", "stat-primary-color", StaTUIInfo.LockStateColors[StaTLockState.Primary], true);
            PrimaryColorSlider.DisplayInMapper = false;
            PrimaryColorSlider.ValueChanged += ChangePrimaryColor;

            SecondaryColorSlider = blockBehaviour.AddColourSlider(Mod.isJapanese ? "二次ロック" : "SecondaryLock Color", "stat-secondary-color", StaTUIInfo.LockStateColors[StaTLockState.Secondary], true);
            SecondaryColorSlider.DisplayInMapper = false;
            SecondaryColorSlider.ValueChanged += ChangeSecondaryColor;

            //画像読み込み
            LockAreaTexture = ModTexture.GetTexture("LockAreaIcon");   //ロック可能領域のアイコン
            LockAreaTexture.wrapMode = TextureWrapMode.Clamp;    //端は区切る
            LockAreaIcon = Sprite.Create(LockAreaTexture, new Rect(0, 0, LockAreaTexture.width, LockAreaTexture.height), Vector2.zero);   //スプライト生成

            ///<summary>
            ///UIの初期化
            /// </summary>
            if(LockAreaObject == null)
            {
                LockAreaObject = new GameObject("LockAreaIcon", typeof(RectTransform), typeof(Image));
                LockAreaObject.transform.SetParent(Mod.StaTMod.transform);
                LockAreaObject.SetActive(false);

                RectTransform LockAreaRect = LockAreaObject.GetComponent<RectTransform>();
                LockAreaRect.sizeDelta = new Vector2(1920f, 1080f);
                LockAreaRect.anchorMin = new Vector2(0.5f, 0.5f);
                LockAreaRect.anchorMax = new Vector2(0.5f, 0.5f);
                LockAreaRect.anchoredPosition = new Vector2(0, 0);
                LockAreaRect.localScale = Vector3.one * 1f;

                LockAreaImage = LockAreaObject.GetComponent<Image>();
                LockAreaImage.sprite = LockAreaIcon;
                LockAreaImage.color = StaTUIInfo.LockStateColors[StaTLockState.None];
            }

            //シミュ中
            if (!blockBehaviour.isBuildBlock)
            {
                //rigidbodyを取得
                rigidbody = GetComponent<Rigidbody>();

                //プレイヤーとブロックの持ち主が一致するか
                bool isMP;
                localIsOwner = LocalIsOwner(out isMP);

                //一致する場合はチーム情報をTargetControllerに渡す
                if (localIsOwner)
                {
                    //バレンなどの場合はチームが無いため、便宜的にNone扱いとする
                    if(isMP)
                    {
                        Player Player = Player.From(blockBehaviour.ParentMachine.PlayerID);
                        StaTTargetController.MyTeam = Player.Team;
                    }
                    else
                    {
                        StaTTargetController.MyTeam = MPTeam.None;
                    }

                    //TargetControllerが初期化されていない場合は初期化（ついでにこれでリスポ時は動かない）
                    if (!StaTTargetController.init)
                    {
                        StaTTargetController.SimulationStartInit();
                        StaTTargetControllerHost.SimulationStartInit();

                        Instance = this;
                    }
                }
            }
        }

        public void Start()
        {
            if(Instance == this)
            {
                //音量変更
                StaTSoundController.Instance.ChangeVolume(VolumeSlider.Value);
            }
        }

        /// <summary>
        /// シミュ中は毎フレームCorePosition, CoreSpeedを更新
        /// </summary>
        public void Update()
        {
            //建築中は無視
            if(blockBehaviour.isBuildBlock)
            {
                ActivateUI = false;
                LockAreaObject.SetActive(false);
                return;
            }

            //プレイヤーとブロックが一致している場合、自分の座標とターゲット切替えキーが押されたかをTargetControllerに渡す
            if(localIsOwner)
            {
                StaTTargetController.CorePosition = transform.position;

                if(TargetChangeKey.IsPressed || TargetChangeKey.EmulationPressed())
                {
                    StaTTargetController.TargetChangePressed = true;
                }

                if(ActivateUIKey.IsPressed || ActivateUIKey.EmulationPressed())
                {
                    ActivateUI = !ActivateUI;

                    LockAreaObject.SetActive(ActivateUI);
                }
            }   
        }

        /// <summary>
        /// プレイヤーのIDとブロックの持ち主のIDを比べる関数
        /// </summary>
        public bool LocalIsOwner(out bool isMP)
        {
            ushort BlockPlayerID, OwnerID;

            if(!StatMaster.isMP)
            {
                isMP = false;

                return true;
            }
            else if(StatMaster.PlayMode == BesiegePlayMode.Spectator)
            {
                isMP = true;

                return false;
            }
            else
            {
                isMP = true;

                BlockPlayerID = blockBehaviour.ParentMachine.PlayerID;
                OwnerID = PlayerMachine.GetLocal().Player.NetworkId;

                return BlockPlayerID == OwnerID ? true : false;
            }
        }

        public void AppearUIConfig(bool value)
        {
            BackgroundColorSlider.DisplayInMapper = value;
            NoneColorSlider.DisplayInMapper = value;
            PrimaryColorSlider.DisplayInMapper = value;
            SecondaryColorSlider.DisplayInMapper = value;
        }

        public void ChangeBackgroundColor(Color color)
        {
            LockAreaImage.color = color;
        }

        public void ChangeNoneColor(Color color)
        {
            StaTUIInfo.LockStateColors[StaTLockState.None] = color;
        }

        public void ChangePrimaryColor(Color color)
        {
            StaTUIInfo.LockStateColors[StaTLockState.Primary] = color;
        }

        public void ChangeSecondaryColor(Color color)
        {
            StaTUIInfo.LockStateColors[StaTLockState.Secondary] = color;
        }
    }
}
