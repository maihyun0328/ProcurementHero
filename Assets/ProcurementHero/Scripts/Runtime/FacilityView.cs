using System;
using System.Collections;
using System.Collections.Generic;
using ProcurementHero.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcurementHero
{
    /// <summary>
    /// 시설 하나를 기본 도형으로 그리고, 구매 결과에 따라 변화를 보여 준다.
    /// 물품 수준은 0(없음) · 1(부분 개선) · 2(완전 해결)이며 그 사이 값은 전환 애니메이션에 쓴다.
    /// </summary>
    public class FacilityView : MonoBehaviour
    {
        public const float StageWidth = 800f;
        public const float StageHeight = 560f;

        public static int LevelOf(ItemOutcome outcome) => outcome == ItemOutcome.Full ? 2 : 1;

        FacilityKind kind;
        RectTransform stage;
        float levelA, levelB;
        Coroutine sequence;
        Action pendingFinish;
        float targetA, targetB;
        string finalBubble;

        // 공통
        readonly List<Resident> residents = new List<Resident>();
        RectTransform truck;
        RectTransform boxes;
        Image bubble;
        TextMeshProUGUI bubbleText;
        float noteTimer, dropTimer;

        // 학교
        Image hallWindow, hallShade, speaker;
        readonly List<Image> lamps = new List<Image>();
        readonly List<Image> flags = new List<Image>();
        // 보건소
        Image sun, ac, dispenser, bench, cup;
        readonly List<Image> heatWaves = new List<Image>();
        readonly List<Image> breezes = new List<Image>();
        // 대피소
        Image water, waterSurface, pump, beacon, beaconGlow, outflow;
        readonly List<Image> windows = new List<Image>();
        readonly List<RectTransform> rainDrops = new List<RectTransform>();

        class Resident
        {
            public RectTransform root;
            public Image head;
            public Image mouth;
            public TextMeshProUGUI mark;
            public Vector2 home;
            public float phase;
        }

        public bool IsPlaying => sequence != null;

        public static FacilityView Create(Transform parent, FacilityKind kind)
        {
            var root = UIKit.Rect(parent, "Facility_" + kind);
            root.Fill();
            var view = root.gameObject.AddComponent<FacilityView>();
            view.Build(kind);
            return view;
        }

        void Build(FacilityKind facilityKind)
        {
            kind = facilityKind;
            stage = UIKit.Rect(transform, "Stage");
            stage.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(StageWidth, StageHeight));
            // 무대 밖(대기 중인 배송 차량, 빗방울)은 잘라 낸다.
            stage.gameObject.AddComponent<RectMask2D>();

            // 하늘 · 풀밭 · 길
            Rect("Sky", ShapeSprites.Square, Theme.Sky, 0, 40, StageWidth, StageHeight - 80);
            Rect("Grass", ShapeSprites.Square, Theme.Grass, 0, -200, StageWidth, 160);
            Rect("Road", ShapeSprites.Square, Theme.Road, 0, -225, StageWidth, 46);
            for (int i = 0; i < 8; i++)
                Rect("RoadLine", ShapeSprites.Square, Theme.Paper, -350 + i * 100, -225, 40, 5);

            switch (kind)
            {
                case FacilityKind.School: BuildSchool(); break;
                case FacilityKind.Clinic: BuildClinic(); break;
                default: BuildShelter(); break;
            }

            BuildResidents();
            BuildTruck();

            bubble = Rect("Bubble", ShapeSprites.Rounded, Theme.Paper, 0, 215, 560, 84);
            bubble.gameObject.AddComponent<Outline>().effectColor = Theme.WithAlpha(Theme.Navy, 0.35f);
            bubbleText = UIKit.Text(bubble.transform, "Text", "", 25, Theme.Navy, TextAlignmentOptions.Center);
            bubbleText.rectTransform.Fill(16, 16, 6, 6);
            bubble.gameObject.SetActive(false);

            Render();
        }

        // ───────── 시설별 구성 ─────────

        void BuildSchool()
        {
            Rect("Wall", ShapeSprites.Rounded, Theme.SchoolWall, 20, -20, 470, 270);
            Rect("Roof", ShapeSprites.Triangle, Theme.Roof, 20, 160, 530, 110);
            for (int i = 0; i < 7; i++)
            {
                var flag = Rect("Flag", ShapeSprites.Triangle,
                    i % 3 == 0 ? Theme.Teal : i % 3 == 1 ? Theme.Warning : Theme.Roof, -190 + i * 70, 96, 34, 30);
                flag.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
                flags.Add(flag);
            }
            hallWindow = Rect("HallWindow", ShapeSprites.Rounded, Theme.WindowLit, 20, 10, 330, 150);
            for (int i = 0; i < 3; i++)
            {
                var glow = Rect("LampGlow", ShapeSprites.Glow, Theme.WithAlpha(Color.white, 0.9f), -80 + i * 100, 55, 120, 120);
                lamps.Add(glow);
                Rect("Lamp", ShapeSprites.Circle, Theme.Paper, -80 + i * 100, 70, 22, 22);
            }
            hallShade = Rect("HallShade", ShapeSprites.Rounded, Theme.Navy, 20, 10, 330, 150);
            Label("강당", 22, Theme.Navy, 20, -92, 200, 32, true);
            Rect("Door", ShapeSprites.Rounded, Theme.Roof, -150, -122, 70, 66);
            speaker = Rect("Speaker", ShapeSprites.Rounded, Theme.Navy, 300, -60, 56, 90);
            Rect("SpeakerCone", ShapeSprites.Circle, Theme.NavySoft, 0, 10, 34, 34).transform.SetParent(speaker.transform, false);
            Rect("SpeakerPole", ShapeSprites.Square, Theme.NavySoft, 300, -130, 8, 60);
        }

        void BuildClinic()
        {
            sun = Rect("Sun", ShapeSprites.Glow, Theme.Heat, -300, 190, 200, 200);
            Rect("SunCore", ShapeSprites.Circle, Theme.Hex(0xFFC24B), -300, 190, 80, 80);
            Rect("Wall", ShapeSprites.Rounded, Theme.ClinicWall, 20, -20, 440, 260);
            Rect("Roof", ShapeSprites.Rounded, Theme.TealDark, 20, 118, 470, 34);
            Rect("CrossH", ShapeSprites.Rounded, Theme.Teal, 20, 60, 70, 22);
            Rect("CrossV", ShapeSprites.Rounded, Theme.Teal, 20, 60, 22, 70);
            Rect("WindowL", ShapeSprites.Rounded, Theme.Window, -120, 10, 90, 70);
            Rect("WindowR", ShapeSprites.Rounded, Theme.Window, 160, 10, 90, 70);
            Rect("Door", ShapeSprites.Rounded, Theme.TealDark, 20, -115, 76, 80);
            Label("보건소", 22, Theme.Navy, 20, 2, 200, 32, true);

            ac = Rect("AirConditioner", ShapeSprites.Rounded, Theme.Paper, 160, 82, 120, 40);
            Rect("AcGrille", ShapeSprites.Square, Theme.NavySoft, 0, -8, 96, 4).transform.SetParent(ac.transform, false);
            for (int i = 0; i < 3; i++)
                breezes.Add(Rect("Breeze", ShapeSprites.Rounded, Theme.Cool, 160, 40 - i * 22, 90, 8));

            bench = Rect("Bench", ShapeSprites.Rounded, Theme.Hex(0xB57A4A), -230, -150, 170, 20);
            dispenser = Rect("WaterDispenser", ShapeSprites.Rounded, Theme.Paper, 290, -112, 56, 92);
            Rect("Bottle", ShapeSprites.Circle, Theme.Cool, 0, 50, 44, 44).transform.SetParent(dispenser.transform, false);
            Rect("Tap", ShapeSprites.Square, Theme.Teal, 0, -6, 18, 10).transform.SetParent(dispenser.transform, false);

            for (int i = 0; i < 4; i++)
                heatWaves.Add(Rect("HeatWave", ShapeSprites.Rounded, Theme.WithAlpha(Theme.Heat, 0.6f), -330 + i * 210, -150, 70, 8));
        }

        void BuildShelter()
        {
            for (int i = 0; i < 4; i++)
                Rect("Cloud", ShapeSprites.Circle, Theme.Hex(0x9AA6B8), -300 + i * 200, 250, 260, 130);
            Rect("Wall", ShapeSprites.Rounded, Theme.ShelterWall, 20, -10, 450, 250);
            Rect("Roof", ShapeSprites.Triangle, Theme.RoofBlue, 20, 160, 510, 110);
            for (int i = 0; i < 3; i++)
                windows.Add(Rect("Window", ShapeSprites.Rounded, Theme.Window, -110 + i * 130, 40, 86, 64));
            Rect("Door", ShapeSprites.Rounded, Theme.RoofBlue, 20, -90, 80, 90);
            Label("대피소", 22, Theme.Navy, 20, -18, 200, 32, true);
            beaconGlow = Rect("BeaconGlow", ShapeSprites.Glow, Theme.WithAlpha(Theme.WindowLit, 0.9f), 20, 230, 150, 150);
            beacon = Rect("Beacon", ShapeSprites.Circle, Theme.Warning, 20, 222, 34, 34);
            pump = Rect("Pump", ShapeSprites.Rounded, Theme.Teal, 300, -110, 80, 70);
            Rect("Pipe", ShapeSprites.Square, Theme.TealDark, 60, -22, 60, 12).transform.SetParent(pump.transform, false);
            outflow = Rect("Outflow", ShapeSprites.Rounded, Theme.Water, 392, -170, 16, 70);
            UIKit.Text(pump.transform, "Label", "배수", 18, Color.white, TextAlignmentOptions.Center, true).rectTransform.Fill();

            for (int i = 0; i < 36; i++)
            {
                var drop = Rect("Rain", ShapeSprites.Rounded, Theme.WithAlpha(Theme.Cool, 0.8f),
                    UnityEngine.Random.Range(-StageWidth / 2, StageWidth / 2), UnityEngine.Random.Range(-250, 250), 6, 26);
                rainDrops.Add(drop.rectTransform);
            }
            water = Rect("Water", ShapeSprites.Square, Theme.Water, 0, -280, StageWidth, 150);
            water.rectTransform.pivot = new Vector2(0.5f, 0f);
            water.rectTransform.anchoredPosition = new Vector2(0, -StageHeight / 2);
            waterSurface = Rect("WaterSurface", ShapeSprites.Rounded, Theme.WithAlpha(Color.white, 0.5f), 0, 0, StageWidth, 8);
        }

        void BuildResidents()
        {
            Vector2[] spots = kind switch
            {
                FacilityKind.School => new[] { new Vector2(-300, -150), new Vector2(-220, -160), new Vector2(200, -160) },
                FacilityKind.Clinic => new[] { new Vector2(-270, -150), new Vector2(-190, -150), new Vector2(140, -165) },
                _ => new[] { new Vector2(-290, -40), new Vector2(-220, -40), new Vector2(160, -40) }
            };
            Color[] shirts = { Theme.Teal, Theme.Warning, Theme.Hex(0x8E7CC3) };

            if (kind == FacilityKind.Shelter)
                Rect("Platform", ShapeSprites.Rounded, Theme.Hex(0xC9B48F), -255, -78, 190, 26);

            for (int i = 0; i < spots.Length; i++)
            {
                var r = new Resident { home = spots[i], phase = i * 1.3f };
                r.root = UIKit.Rect(stage, "Resident");
                r.root.Place(new Vector2(0.5f, 0.5f), spots[i], new Vector2(50, 90));
                UIKit.Shape(r.root, "Body", ShapeSprites.Rounded, shirts[i], new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(44, 52));
                r.head = UIKit.Shape(r.root, "Head", ShapeSprites.Circle, Theme.Skin, new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(38, 38));
                UIKit.Shape(r.head.transform, "EyeL", ShapeSprites.Circle, Theme.Navy, new Vector2(0.5f, 0.5f), new Vector2(-7, 4), new Vector2(5, 5));
                UIKit.Shape(r.head.transform, "EyeR", ShapeSprites.Circle, Theme.Navy, new Vector2(0.5f, 0.5f), new Vector2(7, 4), new Vector2(5, 5));
                r.mouth = UIKit.Shape(r.head.transform, "Mouth", ShapeSprites.Rounded, Theme.Roof, new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(8, 4));
                r.mark = UIKit.Text(r.root, "Mark", "…", 26, Theme.NavySoft, TextAlignmentOptions.Center, true);
                r.mark.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, 18), new Vector2(60, 34));
                residents.Add(r);
            }
        }

        void BuildTruck()
        {
            truck = UIKit.Rect(stage, "Truck");
            truck.Place(new Vector2(0.5f, 0.5f), new Vector2(-560, -205), new Vector2(200, 100));
            UIKit.Shape(truck, "Cargo", ShapeSprites.Rounded, Theme.Teal, new Vector2(0.5f, 0.5f), new Vector2(-25, 10), new Vector2(140, 70));
            UIKit.Shape(truck, "Cab", ShapeSprites.Rounded, Theme.TealDark, new Vector2(0.5f, 0.5f), new Vector2(70, 0), new Vector2(56, 50));
            UIKit.Shape(truck, "Glass", ShapeSprites.Rounded, Theme.Sky, new Vector2(0.5f, 0.5f), new Vector2(76, 8), new Vector2(30, 20));
            UIKit.Shape(truck, "WheelL", ShapeSprites.Circle, Theme.Navy, new Vector2(0.5f, 0.5f), new Vector2(-55, -32), new Vector2(34, 34));
            UIKit.Shape(truck, "WheelR", ShapeSprites.Circle, Theme.Navy, new Vector2(0.5f, 0.5f), new Vector2(60, -32), new Vector2(34, 34));
            var label = UIKit.Text(truck, "Label", "새봄 배송", 20, Color.white, TextAlignmentOptions.Center, true);
            label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-25, 10), new Vector2(140, 40));

            boxes = UIKit.Rect(stage, "Boxes");
            boxes.Place(new Vector2(0.5f, 0.5f), new Vector2(-60, -170), new Vector2(120, 60));
            UIKit.Shape(boxes, "BoxA", ShapeSprites.Rounded, Theme.Hex(0xD9A066), new Vector2(0.5f, 0.5f), new Vector2(-28, 0), new Vector2(50, 46));
            UIKit.Shape(boxes, "BoxB", ShapeSprites.Rounded, Theme.Hex(0xC98B50), new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(50, 46));
            UIKit.Text(boxes, "A", "A", 22, Color.white, TextAlignmentOptions.Center, true).rectTransform
                .Place(new Vector2(0.5f, 0.5f), new Vector2(-28, 0), new Vector2(50, 46));
            UIKit.Text(boxes, "B", "B", 22, Color.white, TextAlignmentOptions.Center, true).rectTransform
                .Place(new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(50, 46));
            boxes.gameObject.SetActive(false);
        }

        // ───────── 상태 ─────────

        /// <summary>연출 없이 즉시 상태를 맞춘다. 진행 중인 연출은 멈춘다.</summary>
        public void SetState(int a, int b, string bubbleMessage = null)
        {
            StopSequence();
            levelA = targetA = a;
            levelB = targetB = b;
            truck.anchoredPosition = new Vector2(-560, -205);
            boxes.gameObject.SetActive(false);
            ShowBubble(bubbleMessage);
            Render();
        }

        /// <summary>배송 차량 이동 → 물품 도착 → 시설 변화 → 주민 반응. onFinished는 건너뛰어도 한 번만 불린다.</summary>
        public void PlayDelivery(int a, int b, string reaction, Action<Sfx> sound, Action onFinished)
        {
            StopSequence();
            targetA = a;
            targetB = b;
            finalBubble = reaction;
            pendingFinish = onFinished;
            sequence = StartCoroutine(DeliveryRoutine(sound));
        }

        /// <summary>연출을 건너뛰고 최종 상태를 즉시 적용한다.</summary>
        public void Skip()
        {
            if (sequence == null) return;
            StopCoroutine(sequence);
            sequence = null;
            levelA = targetA;
            levelB = targetB;
            truck.anchoredPosition = new Vector2(-560, -205);
            boxes.gameObject.SetActive(false);
            ShowBubble(finalBubble);
            Render();
            Finish();
        }

        void StopSequence()
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
            pendingFinish = null;
        }

        void Finish()
        {
            var cb = pendingFinish;
            pendingFinish = null;
            cb?.Invoke();
        }

        IEnumerator DeliveryRoutine(Action<Sfx> sound)
        {
            ShowBubble(null);
            boxes.gameObject.SetActive(false);
            Vector2 start = new Vector2(-560, -205), stop = new Vector2(-170, -205), leave = new Vector2(620, -205);
            yield return Move(truck, start, stop, 1.3f);
            sound?.Invoke(Sfx.Arrive);
            boxes.gameObject.SetActive(true);
            boxes.localScale = Vector3.zero;
            yield return Animate(0.35f, t => boxes.localScale = Vector3.one * Ease(t));
            yield return Wait(0.35f);

            float fromA = levelA;
            yield return Animate(0.9f, t => { levelA = Mathf.Lerp(fromA, targetA, Ease(t)); Render(); });
            float fromB = levelB;
            yield return Animate(0.9f, t => { levelB = Mathf.Lerp(fromB, targetB, Ease(t)); Render(); });
            boxes.gameObject.SetActive(false);
            yield return Move(truck, stop, leave, 0.9f);
            truck.anchoredPosition = start;
            ShowBubble(finalBubble);
            yield return Wait(0.4f);
            sequence = null;
            Finish();
        }

        void ShowBubble(string message)
        {
            bool show = !string.IsNullOrEmpty(message);
            bubble.gameObject.SetActive(show);
            if (show) bubbleText.text = message;
        }

        void Render()
        {
            float a = levelA / 2f, b = levelB / 2f;
            switch (kind)
            {
                case FacilityKind.School:
                    hallShade.color = Theme.WithAlpha(Theme.Navy, 0.82f * (1f - a));
                    for (int i = 0; i < lamps.Count; i++)
                        lamps[i].color = Theme.WithAlpha(Color.white, Mathf.Clamp01(levelA - (i == 1 ? 0.2f : 1.2f)) * 0.9f);
                    foreach (var f in flags) SetAlpha(f, a);
                    SetAlpha(speaker, Mathf.Lerp(0.25f, 1f, b));
                    foreach (Transform child in speaker.transform) SetAlpha(child.GetComponent<Image>(), Mathf.Lerp(0.25f, 1f, b));
                    break;
                case FacilityKind.Clinic:
                    float heat = 1f - a;
                    sun.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(120, 220, heat);
                    foreach (var w in heatWaves) SetAlpha(w, 0.7f * heat);
                    SetAlpha(ac, Mathf.Clamp01(levelA));
                    foreach (Transform child in ac.transform) SetAlpha(child.GetComponent<Image>(), Mathf.Clamp01(levelA));
                    SetAlpha(dispenser, Mathf.Clamp01(levelB));
                    foreach (Transform child in dispenser.transform) SetAlpha(child.GetComponent<Image>(), Mathf.Clamp01(levelB));
                    SetAlpha(bench, Mathf.Clamp01(levelA - 1f));
                    if (residents.Count > 2)
                        residents[2].home = Vector2.Lerp(new Vector2(140, -165), new Vector2(240, -165), b);
                    break;
                default:
                    foreach (var w in windows) w.color = Color.Lerp(Theme.Window, Theme.WindowLit, a);
                    SetAlpha(pump, Mathf.Clamp01(levelB));
                    foreach (Transform child in pump.transform) SetAlpha(child.GetComponent<Graphic>(), Mathf.Clamp01(levelB));
                    float h = Mathf.Lerp(170f, 22f, b);
                    water.rectTransform.sizeDelta = new Vector2(StageWidth, h);
                    waterSurface.rectTransform.anchoredPosition = new Vector2(0, -StageHeight / 2 + h);
                    break;
            }

            float mood = (a + b) / 2f;
            foreach (var r in residents)
            {
                r.mouth.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(8, 20, mood), Mathf.Lerp(4, 7, mood));
                r.head.color = kind == FacilityKind.Clinic ? Color.Lerp(Theme.Hex(0xF2A285), Theme.Skin, a) : Theme.Skin;
                if (mood >= 0.99f) { r.mark.text = "★"; r.mark.color = Theme.Warning; }
                else if (mood >= 0.45f) { r.mark.text = "○"; r.mark.color = Theme.Teal; }
                else { r.mark.text = "…"; r.mark.color = Theme.NavySoft; }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float time = Time.time;
            float a = levelA / 2f, b = levelB / 2f, mood = (a + b) / 2f;

            foreach (var r in residents)
            {
                float bounce = mood >= 0.99f ? Mathf.Abs(Mathf.Sin(time * 5f + r.phase)) * 12f : mood >= 0.45f ? Mathf.Abs(Mathf.Sin(time * 2.5f + r.phase)) * 4f : 0f;
                float sit = kind == FacilityKind.Clinic && levelA > 1.5f && r != residents[2] ? -12f : 0f;
                r.root.anchoredPosition = r.home + new Vector2(0, bounce + sit);
            }

            switch (kind)
            {
                case FacilityKind.School:
                    if (levelB > 0.05f)
                    {
                        noteTimer -= dt;
                        if (noteTimer <= 0f)
                        {
                            noteTimer = levelB >= 1.5f ? 0.45f : 1.4f;
                            StartCoroutine(FloatNote());
                        }
                    }
                    if (levelA > 0.05f)
                        foreach (var lamp in lamps)
                            lamp.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(time * 3f) * 0.06f);
                    break;
                case FacilityKind.Clinic:
                    for (int i = 0; i < heatWaves.Count; i++)
                        heatWaves[i].rectTransform.anchoredPosition = new Vector2(-330 + i * 210 + Mathf.Sin(time * 2f + i) * 14f, -150 + Mathf.Sin(time * 3f + i) * 10f);
                    for (int i = 0; i < breezes.Count; i++)
                    {
                        float phase = Mathf.Repeat(time * 0.8f + i * 0.33f, 1f);
                        breezes[i].rectTransform.anchoredPosition = new Vector2(160 - phase * 140f, 50 - i * 22 - phase * 20f);
                        SetAlpha(breezes[i], Mathf.Clamp01(levelA) * (1f - phase) * 0.9f);
                    }
                    break;
                default:
                    foreach (var d in rainDrops)
                    {
                        var p = d.anchoredPosition + new Vector2(-60f, -520f) * dt;
                        if (p.y < -StageHeight / 2) p = new Vector2(UnityEngine.Random.Range(-StageWidth / 2, StageWidth / 2 + 120), StageHeight / 2);
                        d.anchoredPosition = p;
                    }
                    bool on = levelA > 0.05f && (levelA >= 1.5f ? Mathf.Repeat(time, 1f) < 0.6f : Mathf.Repeat(time * 3.3f, 1f) < 0.3f);
                    SetAlpha(beacon, on ? 1f : 0.25f);
                    SetAlpha(beaconGlow, on ? Mathf.Clamp01(levelA) * 0.9f : 0f);
                    SetAlpha(outflow, levelB > 0.05f ? (0.5f + Mathf.Sin(time * 8f) * 0.3f) * Mathf.Clamp01(levelB) : 0f);
                    waterSurface.rectTransform.localScale = new Vector3(1f, 1f + Mathf.Sin(time * 2f) * 0.3f, 1f);
                    break;
            }
        }

        IEnumerator FloatNote()
        {
            var note = UIKit.Text(stage, "Note", "♪", UnityEngine.Random.Range(34f, 46f),
                UnityEngine.Random.value > 0.5f ? Theme.TealDark : Theme.Roof, TextAlignmentOptions.Center, true);
            var rt = note.rectTransform;
            Vector2 from = new Vector2(300 + UnityEngine.Random.Range(-20f, 20f), -10);
            rt.Place(new Vector2(0.5f, 0.5f), from, new Vector2(50, 50));
            float t = 0;
            while (t < 2f)
            {
                t += Time.deltaTime;
                rt.anchoredPosition = from + new Vector2(Mathf.Sin(t * 4f) * 16f - t * 30f, t * 90f);
                note.alpha = 1f - t / 2f;
                yield return null;
            }
            Destroy(note.gameObject);
        }

        // ───────── 도우미 ─────────

        Image Rect(string name, Sprite sprite, Color color, float x, float y, float w, float h)
        {
            return UIKit.Shape(stage, name, sprite, color, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
        }

        void Label(string text, float size, Color color, float x, float y, float w, float h, bool bold)
        {
            UIKit.Text(stage, "Label", text, size, color, TextAlignmentOptions.Center, bold).rectTransform
                .Place(new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
        }

        static void SetAlpha(Graphic g, float alpha)
        {
            if (!g) return;
            var c = g.color;
            c.a = alpha;
            g.color = c;
        }

        static float Ease(float t) => 1f - (1f - t) * (1f - t);

        static IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        static IEnumerator Animate(float duration, Action<float> step)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }

        IEnumerator Move(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            yield return Animate(duration, t => rt.anchoredPosition = Vector2.Lerp(from, to, Ease(t)));
        }

        void LateUpdate()
        {
            var parent = (RectTransform)transform;
            var size = parent.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            float scale = Mathf.Min(size.x / StageWidth, size.y / StageHeight);
            stage.localScale = new Vector3(scale, scale, 1f);
        }

        void OnDisable()
        {
            // 화면 전환으로 꺼질 때 연출이 반쯤 남지 않도록 최종 상태로 맞춘다.
            // 결과는 구매 확정 시점에 이미 저장되므로 완료 콜백은 부르지 않는다.
            if (sequence == null) return;
            pendingFinish = null;
            Skip();
        }
    }
}
