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
		public static bool isJapanese = SingleInstance<LocalisationManager>.Instance.currLangName.Contains("日本語");

		public static Dictionary<string, Transform> StabBaseDict;	//StabBaseをまとめたもの

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

			//各辞書を初期化
			StabBaseDict = new Dictionary<string, Transform> { };

			//各ModuleとBehaviourをセットにし、XML上で使えるように
			Modding.Modules.CustomModules.AddBlockModule<StaTStabBaseBlockModule, StaTStabBaseBlockModuleBehaviour>("StaTStabBaseBlockModule", true);
			Modding.Modules.CustomModules.AddBlockModule<StaTStabSlaveBlockModule, StaTStabSlaveBlockModuleBehaviour>("StaTStabSlaveBlockModule", true);


		}
	}
}
