using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Modding;
using Modding.Modules;
using Modding.Blocks;
using Modding.Common;
using UnityEngine;
using UnityEngine.UI;
using Localisation;

namespace StaTSpace
{
	public class Mod : ModEntryPoint
	{
		public static GameObject StaTMod;
		public static GameObject TargetController;
		public static bool isJapanese = SingleInstance<LocalisationManager>.Instance.currLangName.Contains("日本語");

		public static Dictionary<string, Transform> StabBaseDict;   //StabBaseをまとめたもの

		//Mod.Log(), Mod.Warning(), Mod.Error();
		public static void Log(string msg)
		{
			Debug.Log("StaT Log: " + msg);
		}
		public static void Warning(string msg)
		{
			Debug.LogWarning("StaT Warning: " + msg);
		}
		public static void Error(string msg)
		{
			Debug.LogError("StaT Error: " + msg);
		}

		public override void OnLoad()
		{
			// Called when the mod is loaded.

			//MBSModを作成、シーンチェンジしても壊さないように
			StaTMod = new GameObject("StaTMod");
			UnityEngine.Object.DontDestroyOnLoad(StaTMod);
			Canvas val = StaTMod.AddComponent<Canvas>();
			val.renderMode = RenderMode.ScreenSpaceOverlay;
			val.sortingOrder = 0;
			val.gameObject.layer = LayerMask.NameToLayer("HUD");
			StaTMod.AddComponent<CanvasScaler>().scaleFactor = 1f;   //画面サイズに応じてUIをスケーリングするためのコンポーネントをアタッチする

			TargetController = new GameObject("TargetController");
			TargetController.transform.SetParent(StaTMod.transform);
			TargetController.AddComponent<StaTTargetController>();

			//BlockSelectorをBW2_UIに追加
			SingleInstance<BlockSelector>.Instance.transform.parent = StaTMod.transform;

			//各辞書を初期化
			StabBaseDict = new Dictionary<string, Transform> { };

			//各ModuleとBehaviourをセットにし、XML上で使えるように
			Modding.Modules.CustomModules.AddBlockModule<StaTStabBaseBlockModule, StaTStabBaseBlockModuleBehaviour>("StaTStabBaseBlockModule", true);
			Modding.Modules.CustomModules.AddBlockModule<StaTStabSlaveBlockModule, StaTStabSlaveBlockModuleBehaviour>("StaTStabSlaveBlockModule", true);
			Modding.Modules.CustomModules.AddBlockModule<StaTIFFBlockModule, StaTIFFBlockModuleBehaviour>("StaTIFFBlockModule", true);


		}
	}

	/// <summary>
	/// ブロック設置時にスクリプトを貼り付けるクラス
	/// </summary>
	public class BlockSelector : SingleInstance<BlockSelector>
	{
		// ブロックのIDと、追加したいスクリプトを紐づけた辞書
		public Dictionary<string, Type> BlockDict = new Dictionary<string, Type>
		{
//コアブロック
			{"StartingBlock", typeof(StaTStartingBlockScript) }

		};

		// プロパティ
		public override string Name
		{
			get
			{
				return "StaTBlockSelector";
			}
		}

		public void Awake()
		{
			// ブロックを設置した場合に呼び出されるアクションに、AddScriptというメソッドを追加する
			Events.OnBlockInit += new Action<Block>(AddScript);
		}

		// ブロック設置時に、そのブロックに所定のスクリプトを貼り付ける関数
		public void AddScript(Block block)
		{
			BlockBehaviour internalObject = block.BuildingBlock.InternalObject;

			// そのブロックがスクリプトを貼り付けるべきブロックであるなら、貼り付ける
			if (BlockDict.ContainsKey(internalObject.name))
			{
				Type type = BlockDict[internalObject.name];
				try
				{
					// まだ所定のスクリプトが貼り付けられていない場合にのみ、貼り付ける
					if (internalObject.GetComponent(type) == null)
					{
						internalObject.gameObject.AddComponent(type);
					}
				}
				catch
				{
					Mod.Error("StaT AddScript Error!");
				}
				return;
			}
		}


	}
}
