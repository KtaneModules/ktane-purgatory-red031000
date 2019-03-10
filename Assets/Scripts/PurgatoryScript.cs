using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Random = UnityEngine.Random;

public class PurgatoryScript : MonoBehaviour
{
	public KMBombModule module;
	public KMBombInfo info;
	public new KMAudio audio;
	public KMBossModule BossModule;

	//LED stuff
	public Renderer ledRenderer;
	public Light ledLight;
	public Material[] UnlitMaterials;
	private bool flicker;

	public TextMesh text;

	public KMSelectable HeavenButton, HellButton;

	private Color ledColor;
	private bool _lightsOn, _isSolved;

	private static int _moduleIdCounter = 1;
	private int _moduleId;

	private Destination? repeatedClickDestination;

	private Destination? destination;
	private bool WaitToEnd, TwoInAny;

	private int? RequiredClicks;
	private int ClickCounter;

	private int colorIndex;
	private int stage = 1;

	private Coroutine FlickerRoutine;

	private List<YellowTuple?> scoreList = new List<YellowTuple?>();

	private static readonly string[] _ignoredModulesDefault = {
		"Souvenir",
		"Forget Everything",
		"Forget Me Not",
		"Turn The Key",
		"The Time Keeper",
		"Simon's Stages",
		"Forget This",
		"Timing Is Everything",
		"Purgatory",
		"Alchemy",
		"Cookie Jars"
	};

	private string[] _ignoredModules;

	// Use this for initialization
	void Start ()
	{
		_ignoredModules = BossModule.GetIgnoredModules(module, _ignoredModulesDefault);
		_moduleId = _moduleIdCounter++;
		module.OnActivate += OnActivate;
	}

	private void OnActivate()
	{
		_lightsOn = true;
		GenerateName();
		RunRules();
	}

	private void Awake()
	{
		RandomizeLED();
		HeavenButton.OnInteract += HeavenInteract;
		HellButton.OnInteract += HellInteract;
	}

	private void RunRules()
	{
		int batteries = info.GetBatteryCount();
		bool vowel = info.GetSerialNumber().Any("AEIOU".Contains);
		DebugLog("Stage {0}:", stage);
		if (vowel)
		{
			switch (colorIndex)
			{
				case 0: //red
					destination = batteries >= 2 ? Destination.Hell : Destination.Heaven;
					DebugLog("Destination: {0}", destination == Destination.Heaven ? "Heaven" : "Hell");
					break;
				case 1: //blue
					if (flicker)
					{
						WaitToEnd = true;
						destination = Destination.Hell;
					}
					else if (batteries < 4)
						destination = Destination.Heaven;
					else
						destination = Destination.Hell;

					DebugLog("Destination: {0}", destination == Destination.Heaven ? "Heaven" : "Hell");
					DebugLog("Wait Until End: {0}", WaitToEnd);
					break;
				case 2: //green
					if (info.GetOnIndicators().Count() == 2)
						destination = Destination.Hell;
					else if (text.text.Length <= 5)
					{
						TwoInAny = true;
						destination = Destination.Heaven;
					}
					else destination = Destination.Hell;
					DebugLog("Destination: {0}", destination == Destination.Heaven ? "Heaven" : "Hell");
					DebugLog("Two Required: {0}", TwoInAny);
					break;
				case 3: //yellow
					WaitToEnd = true;
					int scoreZero = 0;
					int scoreOne = 0;
					int scoreTwo = 0;
					if (info.IsPortPresent(Port.Parallel))
					{
						scoreZero += 1;
						scoreOne -= 1;
						scoreTwo -= 2;
					}

					if (batteries > 2)
					{
						scoreZero -= 1;
						scoreOne -= 2;
						scoreTwo += 1;
					}

					if (info.IsIndicatorOn(Indicator.SIG))
					{
						scoreZero += 1;
						scoreOne += 1;
						scoreTwo -= 1;
					}

					if (info.IsIndicatorOff(Indicator.SIG))
					{
						scoreZero -= 1;
						scoreOne -= 2;
						scoreTwo -= 1;
					}

					if (flicker)
					{
						scoreZero += 1;
						scoreOne -= 2;
						scoreTwo -= 1;
					}

					scoreList.AddRange(new List<YellowTuple?>
					{
						new YellowTuple(0, scoreZero, scoreZero >= 0 ? Destination.Heaven : Destination.Hell),
						new YellowTuple(1, scoreOne, scoreOne >= 0 ? Destination.Heaven : Destination.Hell),
						new YellowTuple(2, scoreTwo, scoreTwo >= 0 ? Destination.Heaven : Destination.Hell)
					});
					DebugLog("0 Strikes:");
					DebugLog("Score: {0}", scoreZero);
					DebugLog("Destination: {0}", scoreZero < 0 ? "Hell" : "Heaven");
					DebugLog("1 Strike:");
					DebugLog("Score: {0}", scoreOne);
					DebugLog("Destination: {0}", scoreOne < 0 ? "Hell" : "Heaven");
					DebugLog("2+ Strikes:");
					DebugLog("Score: {0}", scoreTwo);
					DebugLog("Destination: {0}", scoreTwo < 0 ? "Hell" : "Heaven");
					break;
			}
		}
		else
		{
			switch (colorIndex)
			{
				case 0: //red
					if (info.IsIndicatorOn(Indicator.SIG))
						destination = Destination.Hell;
					else if (text.text.Length % 2 == 0)
						destination = Destination.Heaven;
					else goto case 2;

					DebugLog("Destination: {0}", destination == Destination.Heaven ? "Heaven" : "Hell");
					break;
				case 1: //blue
					if (info.IsPortPresent(Port.Parallel) || info.IsPortPresent(Port.Serial))
						destination = Destination.Hell;
					else if (text.text.Length % 2 == 1 && batteries > 2)
						destination = Destination.Heaven;
					else
					{
						destination = Destination.Hell | Destination.Heaven;
						RequiredClicks = info.GetSerialNumberNumbers().Sum();
					}
					DebugLog("Destination: {0}", destination == (Destination)3 ? "Either" : destination == Destination.Heaven ? "Heaven" : "Hell");
					if (RequiredClicks != null)
						DebugLog("Required Number of Clicks: {0}", (int)RequiredClicks);
					break;
				case 2: //green
					if (batteries > 3)
						destination = Destination.Hell;
					else if (batteries < 3)
						destination = Destination.Heaven;
					else destination = Destination.Heaven | Destination.Hell;
					DebugLog("Destination: {0}", destination == (Destination)3 ? "Either" : destination == Destination.Heaven ? "Heaven" : "Hell");
					break;
				case 3: //yellow
					WaitToEnd = true;
					int scoreZero = 0;
					int scoreOne = 0;
					int scoreTwo = 0;
					if (info.IsPortPresent(Port.Parallel))
					{
						scoreZero += 1;
						scoreOne -= 1;
						scoreTwo -= 2;
					}

					if (batteries > 2)
					{
						scoreZero -= 1;
						scoreOne -= 2;
						scoreTwo += 1;
					}

					if (info.IsIndicatorOn(Indicator.SIG))
					{
						scoreZero += 1;
						scoreOne += 1;
						scoreTwo -= 1;
					}

					if (info.IsIndicatorOff(Indicator.SIG))
					{
						scoreZero -= 1;
						scoreOne -= 2;
						scoreTwo -= 1;
					}

					if (flicker)
					{
						scoreZero += 1;
						scoreOne -= 2;
						scoreTwo -= 1;
					}

					scoreList.AddRange(new List<YellowTuple?>
					{
						new YellowTuple(0, scoreZero, scoreZero >= 0 ? Destination.Heaven : Destination.Hell),
						new YellowTuple(1, scoreOne, scoreOne >= 0 ? Destination.Heaven : Destination.Hell),
						new YellowTuple(2, scoreTwo, scoreTwo >= 0 ? Destination.Heaven : Destination.Hell)
					});
					DebugLog("0 Strikes:");
					DebugLog("Score: {0}", scoreZero);
					DebugLog("Destination: {0}", scoreZero < 0 ? "Hell" : "Heaven");
					DebugLog("1 Strike:");
					DebugLog("Score: {0}", scoreOne);
					DebugLog("Destination: {0}", scoreOne < 0 ? "Hell" : "Heaven");
					DebugLog("2+ Strikes:");
					DebugLog("Score: {0}", scoreTwo);
					DebugLog("Destination: {0}", scoreTwo < 0 ? "Hell" : "Heaven");
					break;
			}
		}
	}

	private bool HeavenInteract()
	{
		string time = info.GetFormattedTime();
		HeavenButton.AddInteractionPunch();
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
		if (!_lightsOn || _isSolved)
			return false;
		DebugLog("You pressed: Heaven");
		if (scoreList.Count != 0 || (destination & Destination.Heaven) != 0)
		{
			if (scoreList.Count == 0)
			{
				if (WaitToEnd)
				{
					IEnumerable<string> solvableList = info.GetSolvableModuleNames().Where(x => !_ignoredModules.Contains(x));
					IEnumerable<string> solvedList = info.GetSolvedModuleNames().Where(x => solvableList.Contains(x));
					if (solvedList.Count() >= solvableList.Count()) //if every non-ignored solvable module is solved
						NextStage(WaitToEnd);
					else
					{
						module.HandleStrike();
						DebugLog("Incorrect, you did not wait until the end, Strike!");
						ResetModule();
					}
				}
				else if (TwoInAny)
					if (time.Contains('2'))
						NextStage(WaitToEnd);
					else
					{
						module.HandleStrike();
						DebugLog("Incorrect, there is no two in the timer, current time is {0}, Strike!", time);
						ResetModule();
					}
				else if (RequiredClicks != null)
				{
					if (repeatedClickDestination != Destination.Heaven && repeatedClickDestination != null)
					{
						module.HandleStrike();
						DebugLog("Incorrect, you cannot mix heaven and hell on repeated clicks, Strike!");
						ResetModule();
					}
					else if (ClickCounter == RequiredClicks)
						NextStage(WaitToEnd);
					else ClickCounter++;

					if (repeatedClickDestination == null)
						repeatedClickDestination = Destination.Heaven;
				}
				else NextStage(WaitToEnd);
			}
			else
			{
				IEnumerable<string> solvableList = info.GetSolvableModuleNames().Where(x => !_ignoredModules.Contains(x));
				IEnumerable<string> solvedList = info.GetSolvedModuleNames().Where(x => solvableList.Contains(x));
				int strikes = Mathf.Clamp(info.GetStrikes(), 0, 2);
				// ReSharper disable once PossibleInvalidOperationException
				YellowTuple tuple = scoreList.First(x => x != null && x.Value.Strike == strikes).Value;
				if ((tuple.Destination & Destination.Heaven) != 0 && solvedList.Count() >= solvableList.Count())
					NextStage(WaitToEnd);
				else
				{
					module.HandleStrike();
					DebugLog("Incorrect, Strike!");
					ResetModule();
				}
			}
		}
		else
		{
			module.HandleStrike();
			DebugLog("Incorrect, Strike!");
			ResetModule();
		}
		return false;
	}

	private void NextStage(bool WaitToEnd)
	{
		if (stage == 5 || WaitToEnd)
		{
			module.HandlePass();
			DebugLog("Correct, module solved!");
			_isSolved = true;
			if (FlickerRoutine != null)
				StopCoroutine(FlickerRoutine);
			ledLight.enabled = false;
			ledRenderer.material = UnlitMaterials[4];
			text.text = string.Empty;
		}
		else
		{
			stage++;
			DebugLog("Correct, next stage");
			ResetModule();
		}
	}

	private void ResetModule()
	{
		repeatedClickDestination = null;
		destination = null;
		WaitToEnd = false;
		TwoInAny = false;
		ClickCounter = 0;
		RequiredClicks = null;
		if (FlickerRoutine != null)
			StopCoroutine(FlickerRoutine);
		scoreList.Clear();
		GenerateName();
		RandomizeLED(false);
		ledLight.enabled = true;
		RunRules();
	}

	private bool HellInteract()
	{
		string time = info.GetFormattedTime();
		HellButton.AddInteractionPunch();
		audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
		if (!_lightsOn || _isSolved)
			return false;
		DebugLog("You pressed: Hell");
		if (scoreList.Count != 0 || (destination & Destination.Hell) != 0)
		{
			if (scoreList.Count == 0)
			{
				if (WaitToEnd)
				{
					IEnumerable<string> solvableList = info.GetSolvableModuleNames().Where(x => !_ignoredModules.Contains(x));
					IEnumerable<string> solvedList = info.GetSolvedModuleNames().Where(x => solvableList.Contains(x));
					if (solvedList.Count() >= solvableList.Count()) //if every non-ignored solvable module is solved
						NextStage(WaitToEnd);
					else
					{
						module.HandleStrike();
						DebugLog("Incorrect, you did not wait until the end, Strike!");
						ResetModule();
					}
				}
				else if (TwoInAny)
					if (time.Contains('2'))
						NextStage(WaitToEnd);
					else
					{
						module.HandleStrike();
						DebugLog("Incorrect, there is no two in the timer, current time is {0}, Strike!", time);
						ResetModule();
					}
				else if (RequiredClicks != null)
				{
					if (repeatedClickDestination != Destination.Hell && repeatedClickDestination != null)
					{
						module.HandleStrike();
						DebugLog("Incorrect, you cannot mix heaven and hell on repeated clicks, Strike!");
						ResetModule();
					}
					else if (ClickCounter == RequiredClicks)
						NextStage(WaitToEnd);
					else ClickCounter++;

					if (repeatedClickDestination == null)
						repeatedClickDestination = Destination.Hell;
				}
				else NextStage(WaitToEnd);
			}
			else
			{
				IEnumerable<string> solvableList = info.GetSolvableModuleNames().Where(x => !_ignoredModules.Contains(x));
				IEnumerable<string> solvedList = info.GetSolvedModuleNames().Where(x => solvableList.Contains(x));
				int strikes = Mathf.Clamp(info.GetStrikes(), 0, 2);
				// ReSharper disable once PossibleInvalidOperationException
				YellowTuple tuple = scoreList.First(x => x != null && x.Value.Strike == strikes).Value;
				if ((tuple.Destination & Destination.Hell) != 0 && solvedList.Count() >= solvableList.Count()) //if every non-ignored solvable module is solved
					NextStage(WaitToEnd);
				else
				{
					module.HandleStrike();
					DebugLog("Incorrect, Strike!");
					ResetModule();
				}
			}
		}
		else
		{
			module.HandleStrike();
			DebugLog("Incorrect, Strike!");
			ResetModule();
		}
		return false;
	}

	private void GenerateName()
	{
		text.text = NameList.Names[Random.Range(0, NameList.Names.Length)];
		DebugLog("Name displayed is {0}", text.text);
	}

	private void RandomizeLED(bool minimize = true)
	{
		switch (Random.Range(0, 4))
		{
			case 0:
				ledColor = Color.red;
				ledRenderer.material = UnlitMaterials[0];
				colorIndex = 0;
				DebugLog("LED is red");
				break;
			case 1:
				ledColor = Color.blue;
				ledRenderer.material = UnlitMaterials[1];
				colorIndex = 1;
				DebugLog("LED is blue");
				break;
			case 2:
				ledColor = new Color(0.0f, 0.75f, 0.0f);
				ledRenderer.material = UnlitMaterials[2];
				colorIndex = 2;
				DebugLog("LED is green");
				break;
			case 3:
				ledColor = Color.yellow;
				ledRenderer.material = UnlitMaterials[3];
				colorIndex = 3;
				DebugLog("LED is yellow");
				break;
		}

		ledLight.color = ledColor;
		if (minimize)
			ledLight.range *= transform.lossyScale.x;
		flicker = Random.Range(0, 5) == 0;
		if (!flicker) return;
		DebugLog("LED is flickering");
		FlickerRoutine = StartCoroutine(FlickerCoRoutine());
	}

	private IEnumerator FlickerCoRoutine()
	{
		while (true)
		{
			yield return new WaitForSeconds(Random.Range(0.1f, 2f));
			ledLight.enabled = false;
			ledRenderer.material = UnlitMaterials[4];
			yield return new WaitForSeconds(Random.Range(0.1f, 2f));
			ledLight.enabled = true;
			ledRenderer.material = UnlitMaterials[colorIndex];
		}

	}

	private void DebugLog(string line, params object[] format)
	{
		Debug.LogFormat("[Purgatory #{0}] " + string.Format(line, format), _moduleId);
	}

	[Flags]
	public enum Destination
	{
		Heaven = 1,
		Hell = 2
	}
#pragma warning disable 414
	private readonly string TwitchHelpMessage = "Press Heaven on 2 with '!{0} press heaven on 2'. Press hell 5 times with '!{0} press hellx5'. Press the hell button with '!{0} press hell'.";
#pragma warning restore 414
	private IEnumerator ProcessTwitchCommand(string command)
	{
		DebugLog("command " + command);
		Match match1 = Regex.Match(command, @"\s*press\s+(heaven|hell)\s+on\s+(\d)\s*",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
		Match match2 = Regex.Match(command, @"\s*press\s+(heaven|hell)x(\d+)\s*",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
		Match match3 = Regex.Match(command, @"\s*press\s+(heaven|hell)\s*",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
		if (match1.Success)
		{
			yield return null;
			yield return new WaitUntil(() => info.GetFormattedTime().Contains(match1.Groups[2].Value));
			if (match1.Groups[1].Value.ToLowerInvariant() == "heaven")
				yield return new[] {HeavenButton};
			else
				yield return new[] {HellButton};
		}
		else if (match2.Success)
		{
			yield return null;
			int pressed = 0;
			while (pressed != int.Parse(match2.Groups[2].Value))
			{
				if (match2.Groups[1].Value.ToLowerInvariant() == "heaven")
					yield return new[] {HeavenButton};
				else
					yield return new[] {HellButton};

				yield return new WaitForSeconds(0.1f);
				pressed++;
			}
		}
		else if (match3.Success)
		{
			yield return null;
			if (match3.Groups[1].Value.ToLowerInvariant() == "heaven")
				yield return new[] {HeavenButton};
			else
				yield return new[] {HellButton};
		}
	}
}