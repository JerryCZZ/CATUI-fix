using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;



















[HarmonyPatch]
public class LocalLoadPatch
{
	private const string TAG = "[CATUI]";








	public static bool EnableLocalLoad = true;


	public static readonly List<string> LocallyLoadedConfigs = new List<string>();

	private static bool summaryLogged;


	private static readonly HashSet<string> receivedOverlayConfigs = new HashSet<string>();

	[HarmonyPatch(typeof(WorldStaticData))]
	public class LocalLoadPatch_WorldStaticData
	{
		[HarmonyPostfix]
		[HarmonyPatch("ReceivedConfigFile")]
		public static void ReceivedConfigFile(string _name, byte[] _data)
		{
			if (!EnableLocalLoad)
			{
				return;
			}
			if (_name == null || !(_name.Contains("XUi") || _name.Contains("qualityinfo")))
			{
				return;
			}


			if (_data != null && _data.Length == 0)
			{
				Log.Warning("{0} Server sent an EMPTY config for '{1}'. This usually means the server's XML config is missing or incomplete. Falling back to local file.", TAG, _name);
				TrackLocalLoad(_name);
				SetLoadLocal(_name);
				return;
			}


			if (_data == null)
			{
				Log.Warning("{0} Server did NOT provide config '{1}'. Falling back to local file.", TAG, _name);
				TrackLocalLoad(_name);
				SetLoadLocal(_name);
				return;
			}


			receivedOverlayConfigs.Add(_name);
			Debug.Log("<color=#00FF00>CATUI local load xml name: </color>" + _name);
		}


		[HarmonyPostfix]
		[HarmonyPatch("AllConfigsReceivedAndLoaded")]
		public static void AllConfigsReceivedAndLoaded(ref bool __result)
		{
			if (!EnableLocalLoad)
			{
				return;
			}
			if (__result && !summaryLogged && LocallyLoadedConfigs.Count > 0)
			{
				summaryLogged = true;
				Log.Warning("{0} XML fallback summary: the following server configs were missing/incomplete and were loaded from local files instead: {1}", TAG, string.Join(", ", LocallyLoadedConfigs.ToArray()));
			}
		}





		[HarmonyPrefix]
		[HarmonyPatch(typeof(XmlPatcher), "ApplyConditionalXmlBlocks")]
		public static bool ApplyConditionalXmlBlocks(string _xmlName, XmlFile _xmlFile, XmlPatcher.EEvaluator _evaluator)
		{
			if (!EnableLocalLoad)
			{
				return true;
			}
			if (_evaluator != XmlPatcher.EEvaluator.Client)
			{
				return true;
			}
			if (_xmlName == null || (!_xmlName.Contains("XUi") && !_xmlName.Contains("qualityinfo")))
			{
				return true;
			}

			if (!receivedOverlayConfigs.Remove(_xmlName))
			{
				return true;
			}
			try
			{


				SanitizeBindingEntities(_xmlFile);
				ApplyLocalCatuiPatch(_xmlName, _xmlFile);
			}
			catch (Exception e)
			{
				Log.Error("{0} Failed to overlay local CATUI patch for '{1}': {2}", TAG, _xmlName, e);
			}
			return true;
		}





		private static void SanitizeBindingEntities(XmlFile _xmlFile)
		{
			if (_xmlFile?.XmlDoc?.Root == null)
			{
				return;
			}
			foreach (XElement element in _xmlFile.XmlDoc.Root.DescendantsAndSelf())
			{
				foreach (XAttribute attribute in element.Attributes())
				{
					string value = attribute.Value;
					if (string.IsNullOrEmpty(value) || value.IndexOf('{') < 0)
					{
						continue;
					}
					string cleaned = value;
					for (int pass = 0; pass < 3; pass++)
					{
						string next = cleaned.Replace("&gt;", ">")
							.Replace("&lt;", "<")
							.Replace("&quot;", "\"")
							.Replace("&apos;", "'")
							.Replace("&amp;", "&");
						if (next == cleaned)
						{
							break;
						}
						cleaned = next;
					}
					if (cleaned != value)
					{
						attribute.Value = cleaned;
					}
				}
			}
		}

		private static void ApplyLocalCatuiPatch(string _xmlName, XmlFile _xmlFile)
		{
			foreach (Mod loadedMod in ModManager.GetLoadedMods())
			{
				if (!IsCatuiMod(loadedMod))
				{
					continue;
				}
				string text = loadedMod.Path + "/Config/" + _xmlName + ".xml";
				if (!SdFile.Exists(text))
				{
					continue;
				}
				XmlFile patch = XmlPatcher.ReadPatchXmlWithFixedModFolders(loadedMod, text);
				if (patch == null)
				{
					continue;
				}
				Log.Out("{0} Overlaying local CATUI config '{1}' from mod '{2}' onto server config.", TAG, _xmlName, loadedMod.Name);
				XmlPatcher.PatchXml(_xmlFile, patch.XmlDoc.Root, patch, loadedMod);
			}
		}



		private static bool IsCatuiMod(Mod _mod)
		{
			return (_mod.Name != null && _mod.Name.StartsWith("CATUI", StringComparison.OrdinalIgnoreCase)) ||
				(_mod.DisplayName != null && _mod.DisplayName.StartsWith("CATUI", StringComparison.OrdinalIgnoreCase));
		}

		private static void SetLoadLocal(string _name)
		{
			WorldStaticData.XmlLoadInfo[] xmlsToLoad = WorldStaticData.xmlsToLoad;
			foreach (WorldStaticData.XmlLoadInfo xmlLoadInfo in xmlsToLoad)
			{
				if (xmlLoadInfo.XmlName.Equals(_name))
				{
					xmlLoadInfo.WasReceivedFromServer = WorldStaticData.EClientFileState.LoadLocal;
					xmlLoadInfo.CompressedXmlData = null;
					break;
				}
			}
		}

		private static void TrackLocalLoad(string _name)
		{
			if (!LocallyLoadedConfigs.Contains(_name))
			{
				LocallyLoadedConfigs.Add(_name);
			}
		}
	}
}
