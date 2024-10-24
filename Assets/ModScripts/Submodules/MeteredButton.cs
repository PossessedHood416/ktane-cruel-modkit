﻿using KModkit;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using static ComponentInfo;

public class MeteredButton : Puzzle
{

    Coroutine tickRoutine = null;

    float pressTime;
    int stage = 0;

    float mashTime = 3f;
    bool mashing = false;
    int pressedNum;

    bool animating = false;
    bool meterStarted = false;

    int utilPresses = 0;

    string[] finalActions = new string[3];  

    readonly int[,] table1 = new int[14, 10] {
            {1, 0, 2, 9, 3, 8, 4, 7, 5, 6},
            {6, 1, 0, 2, 9, 3, 8, 4, 7, 5},
            {5, 6, 1, 0, 2, 9, 3, 8, 4, 7},
            {7, 5, 6, 1, 0, 2, 9, 3, 8, 4},
            {4, 7, 5, 6, 1, 0, 2, 9, 3, 8},
            {8, 4, 7, 5, 6, 1, 0, 2, 9, 3},
            {3, 8, 4, 7, 5, 6, 1, 0, 2, 9},
            {9, 3, 8, 4, 7, 5, 6, 1, 0, 2},
            {2, 9, 3, 8, 4, 7, 5, 6, 1, 0},
            {0, 2, 9, 3, 8, 4, 7, 5, 6, 1},
            {1, 2, 3, 4, 5, 6, 7, 8, 9, 0},
            {0, 9, 8, 7, 6, 5, 4, 3, 2, 1},
            {9, 8, 7, 6, 5, 4, 3, 2, 1, 0},
            {2, 4, 6, 8, 0, 1, 3, 5, 7, 9}
        };

    readonly string[,] table2 = new string[10, 10] {
            {"T6", "H5", "M3", "T1", "H7", "M8", "T1", "H2", "M5", "X8"},
            {"X5", "H6", "T1", "M5", "H1", "T9", "M9", "H4", "T8", "M1"},
            {"M5", "T8", "H5", "M3", "T5", "H7", "X6", "T1", "H3", "M6"},
            {"H3", "M7", "M6", "H2", "M3", "T5", "T2", "X4", "T2", "H7"},
            {"T4", "H9", "M9", "H4", "M8", "X9", "T8", "T3", "H5", "M1"},
            {"M1", "M2", "X1", "M9", "T9", "M4", "H3", "H8", "T4", "M3"},
            {"H2", "M1", "T4", "T7", "X3", "H6", "M7", "T5", "H7", "H4"},
            {"T8", "H3", "M2", "X9", "T2", "H1", "H4", "M6", "M1", "T1"},
            {"M9", "X2", "H8", "T8", "M4", "T3", "M6", "H7", "H3", "T8"},
            {"T7", "T4", "M4", "H6", "H7", "M2", "M5", "T9", "X7", "H9"}
        };

    public MeteredButton(CruelModkitScript Module, int ModuleID, ComponentInfo Info, byte Components) : base(Module, ModuleID, Info, Components)
    {
        Debug.LogFormat("[The Cruel Modkit #{0}] Solving Metered Button. Press the ❖ button to activate the timer.", ModuleID);
        GenButton();
        Info.MeterValue = 0d;
        Module.SetMeter();
        Debug.LogFormat("[The Cruel Modkit #{0}] Number display is {1}.", ModuleID, Info.NumberDisplay);

        finalActions[0] = FindAction();
        Debug.LogFormat("[The Cruel Modkit #{0}] The first action to perform is {1}.", ModuleID, finalActions[0]);
        LogInstruction(finalActions[0]);
    }

    public override void OnButtonPress()
    {
        if (Module.IsAnimating() || animating)
            return;

        Module.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, Module.transform);
        Module.Button.GetComponentInChildren<KMSelectable>().AddInteractionPunch(0.25f);

        if (Module.IsModuleSolved() || !meterStarted)
            return;

        if (!Module.IsSolving())
        {
            if (!Module.CheckValidComponents())
            {
                Debug.LogFormat("[The Cruel Modkit #{0}] Strike! The button was pressed when the component selection was [{1}] instead of [{2}].", ModuleID, Module.GetOnComponents(), Module.GetTargetComponents());
                Module.CauseStrike();
                return;
            }

            Module.StartSolve();
        }

        pressTime = Module.Bomb.GetTime();

    }

    public override void OnButtonRelease()
    {
        if (Module.IsAnimating() || animating)
            return;

        Module.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, Module.transform);

        if (Module.IsModuleSolved() || !meterStarted)
            return;

        if (!Module.CheckValidComponents())
            return;

        float releaseTime = Module.Bomb.GetTime();
        float heldTime = Mathf.RoundToInt(Math.Abs(pressTime - releaseTime));

        string actionChar = finalActions[stage][0].ToString();
        int actionNum = Int32.Parse(finalActions[stage][1].ToString());
        double lastDigit = Math.Floor(Module.Bomb.GetTime()) % 10;
        if (actionChar == "T")
        {
            if (heldTime == 0f && lastDigit == actionNum)
            {
                Debug.LogFormat("[The Cruel Modkit #{0}] Button was tapped correctly. Stage {1} passed.", ModuleID, stage+2);
                Module.StartCoroutine(AdvanceStage());
            }
            else
            {
                Module.CauseStrike();
                Debug.LogFormat("[The Cruel Modkit #{0}] Strike! Button was tapped incorrectly at last digit {1} or button was held.", ModuleID, lastDigit);
                Module.StartCoroutine(Strike());
            }
        }
        else if (actionChar == "H") 
        {
            double holdDigit = Math.Floor(pressTime % 10);
            double releaseDigit = Math.Floor(releaseTime % 10);
            if (holdDigit == (actionNum + Module.Bomb.GetBatteryCount()) % 10 && releaseDigit == Math.Abs(actionNum - Module.Bomb.GetPortCount()))
            {
                Debug.LogFormat("[The Cruel Modkit #{0}] Button was held correctly. Stage {1} passed.", ModuleID, stage + 1);
                Module.StartCoroutine(AdvanceStage());
            }
            else
            {
                Module.CauseStrike();
                Debug.LogFormat("[The Cruel Modkit #{0}] Strike! Button was held incorrectly (held on {1}, released on {2}).", ModuleID, holdDigit, releaseDigit);
                Module.StartCoroutine(Strike());
            }
        }
        else if (actionChar == "M")
        {
            pressedNum++;
            if (!mashing) 
                Module.StartCoroutine(MashCount());
        }
        else if (actionChar == "X")
        {
            int multiplyNum = Module.Bomb.GetSerialNumberNumbers().Last();
            if (multiplyNum == 0) multiplyNum = 10;
            if (utilPresses == (multiplyNum * actionNum) % 25)
            {
                Debug.LogFormat("[The Cruel Modkit #{0}] ❖ button was pressed the correct number of times. Stage {1} passed.", ModuleID, stage + 1);
                Module.StartCoroutine(AdvanceStage());
            }
            else
            {
                Module.CauseStrike();
                Debug.LogFormat("[The Cruel Modkit #{0}] Strike! ❖ button was pressed an incorrect number of times ({1} time(s)).", ModuleID, utilPresses);
                Module.StartCoroutine(Strike());
            }
        }
        utilPresses = 0;
    }

    public override void OnUtilityPress()
    {
        if (Module.IsAnimating() || animating)
            return;

        Module.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Module.transform);
        Module.UtilityButton.GetComponentInChildren<KMSelectable>().AddInteractionPunch(0.5f);

        if (Module.IsModuleSolved())
            return;

        if (!Module.IsSolving()) 
        {
            if (!Module.CheckValidComponents())
            {
                Debug.LogFormat("[The Cruel Modkit #{0}] Strike! The ❖ button was pressed when the component selection was [{1}] instead of [{2}].", ModuleID, Module.GetOnComponents(), Module.GetTargetComponents());
                Module.CauseStrike();
                return;
            }

            Module.StartSolve();
        }

        if (meterStarted)
            utilPresses++;

        if (!meterStarted)
            Module.StartCoroutine(MeterRise());

        return;
    }

    string FindAction()
    {
        int[] colorConverter = { 8, 6, 5, 4, 3, 1, 999, 7, 0, 9, 2 };
        int table1Num = table1[Array.IndexOf(ButtonList, Info.ButtonText), colorConverter[Info.Button]];
        string table2Action = table2[table1Num, Info.NumberDisplay];

        return table2Action;
    }

    IEnumerator AdvanceStage()
    {
        stage++;

        if (stage == 3)
        {
            Debug.LogFormat("[The Cruel Modkit #{0}] Module solved.", ModuleID);
            Module.Solve();
            Module.StopCoroutine(tickRoutine);
            yield break;
        }

        animating = true;
        Info.NumberDisplay = Random.Range(0, 10);
        Debug.LogFormat("[The Cruel Modkit #{0}] Number display is {1}.", ModuleID, Info.NumberDisplay);
        Module.WidgetText[2].text = Info.NumberDisplay.ToString();
        yield return Module.StartCoroutine(Module.HideComponent(CruelModkitScript.ComponentsEnum.Button));
        GenButton();
        Module.StartCoroutine(Module.ShowComponent(CruelModkitScript.ComponentsEnum.Button));

        finalActions[stage] = FindAction();
        Debug.LogFormat("[The Cruel Modkit #{0}] The next action to perform is {1}.", ModuleID, finalActions[stage]);
        LogInstruction(finalActions[stage]);
        animating = false;
    }

    IEnumerator Strike()
    {
        stage = 0;
        animating = true;
        Info.NumberDisplay = Random.Range(0, 10);
        Debug.LogFormat("[The Cruel Modkit #{0}] Number display is {1}.", ModuleID, Info.NumberDisplay);

        Module.StopCoroutine(tickRoutine);
        Info.MeterValue = 0d;
        Module.SetMeter();
        meterStarted = false;

        Module.WidgetText[2].text = Info.NumberDisplay.ToString();
        yield return Module.StartCoroutine(Module.HideComponent(CruelModkitScript.ComponentsEnum.Button));
        GenButton();
        Module.StartCoroutine(Module.ShowComponent(CruelModkitScript.ComponentsEnum.Button));

        finalActions = new string[3];
        finalActions[0] = FindAction();
        Debug.LogFormat("[The Cruel Modkit #{0}] The first action to perform is {1}.", ModuleID, finalActions[0]);
        LogInstruction(finalActions[0]);
        animating = false;
    }

    void GenButton()
    {
        Info.ButtonText = ButtonList[Random.Range(0, 14)];
        int newCol = Random.Range(0, 11);
        while (newCol == 6)  newCol = Random.Range(0, 11);
        Info.Button = newCol;
        Module.SetButton();
        Debug.LogFormat("[The Cruel Modkit #{0}] Button is {1}.", ModuleID, Info.GetButtonInfo());
    }

    void LogInstruction(string Instruction)
    {
        int InstructionNumber = Convert.ToInt32(Instruction.Substring(1));
        switch (Instruction[0])
        {
            case 'T':
                Debug.LogFormat("[The Cruel Modkit #{0}] Tap the button when the last digit of the bomb timer is {1}.", ModuleID, InstructionNumber);
                break;
            case 'H':
                int BatteryCount = Module.Bomb.GetBatteryCount();
                int PortCount = Module.Bomb.GetPortCount();
                Debug.LogFormat("[The Cruel Modkit #{0}] The battery count is {1} and the port count is {2}.", ModuleID, BatteryCount, PortCount);
                Debug.LogFormat("[The Cruel Modkit #{0}] Hold the button when the last digit of the bomb timer is {1}, and release when it's {2}.", ModuleID, (InstructionNumber + BatteryCount) % 10, Math.Abs(InstructionNumber - PortCount));
                break;
            case 'M':
                Debug.LogFormat("[The Cruel Modkit #{0}] Mash the button {1} time(s) across 3 seconds.", ModuleID, InstructionNumber);
                break;
            case 'X':
                int LastSerialDigit = Module.Bomb.GetSerialNumberNumbers().Last();
                Debug.LogFormat("[The Cruel Modkit #{0}] Press the ❖ button {1} time(s), then tap the button.", ModuleID, (InstructionNumber * (LastSerialDigit == 0 ? 10 : LastSerialDigit)) % 25);
                break;
        }
    }

    IEnumerator MeterRise()
    {
        animating = true;
        meterStarted = true;
        double meterLevel;

        float elapsed = 0f;
        float duration = 1f;
        while (elapsed < duration)
        {
            meterLevel = Easing.OutQuad(elapsed, 0, 1, duration);
            Info.MeterValue = meterLevel;
            Module.SetMeter();
            yield return null;
            elapsed += Time.deltaTime;
        }
        animating = false;
        tickRoutine = Module.StartCoroutine(MeterTick());
    }

    IEnumerator MeterTick()
    {
        double meterLevel;
        double meterTime = 90f;
        while (meterTime > 0f)
        {
            meterTime -= Time.deltaTime;
            meterLevel = meterTime / 90f;
            Info.MeterValue = meterLevel;
            Module.SetMeter();
            yield return null;
        }

        meterStarted = false;
        Debug.LogFormat("[The Cruel Modkit #{0}] Strike! The meter has ran out.", ModuleID);
        Module.CauseStrike();
        Module.StartCoroutine(Strike());

    }

    IEnumerator MashCount()
    {
        mashing = true;
        while (mashTime > 0f)
        {
            mashTime -= Time.deltaTime;
            yield return null;
        }

        mashTime = 3;
        mashing = false;

        if (pressedNum == Int32.Parse(finalActions[stage][1].ToString()))
        {
            Debug.LogFormat("[The Cruel Modkit #{0}] Button was mashed the correct number of times. Stage {1} passed.", ModuleID, stage + 1);
            Module.StartCoroutine(AdvanceStage());
        }
        else
        {
            Module.CauseStrike();
            Debug.LogFormat("[The Cruel Modkit #{0}] Strike! Button was mashed an incorrect number of times ({1} time(s)).", ModuleID, pressedNum);
            Module.StartCoroutine(Strike());
        }
        pressedNum = 0;
    }

}
