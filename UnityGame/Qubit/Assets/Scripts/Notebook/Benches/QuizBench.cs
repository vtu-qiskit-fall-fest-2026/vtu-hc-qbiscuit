using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Multiple-choice "decoder calibration" built from the notebook's check-your-understanding questions.</summary>
public class QuizBench : NotebookBench
{
    [System.Serializable]
    public class Question
    {
        [TextArea] public string question;
        public string[] options;
        public int correct;
        [TextArea] public string explain;
    }

    public List<Question> questions = new List<Question>();

    [SerializeField] TMP_Text progressText;
    [SerializeField] TMP_Text questionText;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] Button[] optionButtons = new Button[4];
    [SerializeField] Button nextButton;

    int index, score;
    bool answered;

    void Awake()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int k = i;
            Bind(optionButtons[i], () => Answer(k));
        }
        Bind(nextButton, Next);
    }

    public override void ResetBench()
    {
        index = 0;
        score = 0;
        Show();
    }

    void Show()
    {
        answered = false;
        var nextLabel = nextButton.GetComponentInChildren<TMP_Text>();

        if (index >= questions.Count)
        {
            progressText.text = $"SCORE {score}/{questions.Count}";
            questionText.text = "CALIBRATION COMPLETE";
            foreach (var b in optionButtons) b.gameObject.SetActive(false);
            feedbackText.text = score == questions.Count
                ? $"<color={Hex(teal)}>Decoder calibrated: {score}/{questions.Count}. You're ready for the diagnostic console.</color>"
                : $"Decoder calibrated: {score}/{questions.Count}. Re-read the entries you missed, then run it again.";
            nextLabel.text = "RESTART";
            nextButton.gameObject.SetActive(true);
            answered = true;
            return;
        }

        var q = questions[index];
        progressText.text = $"Q {index + 1}/{questions.Count}   SCORE {score}";
        questionText.text = q.question;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool used = i < q.options.Length;
            optionButtons[i].gameObject.SetActive(used);
            if (!used) continue;
            optionButtons[i].interactable = true;
            optionButtons[i].GetComponentInChildren<TMP_Text>().text = $"[{i + 1}]  {q.options[i]}";
            Paint(optionButtons[i], dim, amber);
        }
        feedbackText.text = "";
        nextLabel.text = "NEXT";
        nextButton.gameObject.SetActive(false);
    }

    public void Answer(int i)
    {
        if (answered || index >= questions.Count) return;
        var q = questions[index];
        if (i >= q.options.Length) return;
        answered = true;

        bool right = i == q.correct;
        if (right) score++;
        for (int k = 0; k < q.options.Length; k++) optionButtons[k].interactable = false;
        Paint(optionButtons[q.correct], teal, teal);
        if (!right) Paint(optionButtons[i], alert, alert);

        feedbackText.text = (right ? $"<color={Hex(teal)}>CORRECT.</color> " : $"<color={Hex(alert)}>NOT QUITE.</color> ") + q.explain;
        progressText.text = $"Q {index + 1}/{questions.Count}   SCORE {score}";
        nextButton.gameObject.SetActive(true);
    }

    public void Next()
    {
        if (index >= questions.Count) { ResetBench(); return; }
        if (!answered) return;
        index++;
        Show();
    }

    public override void HandleKeys(Keyboard kb)
    {
        if (kb.digit1Key.wasPressedThisFrame) Answer(0);
        else if (kb.digit2Key.wasPressedThisFrame) Answer(1);
        else if (kb.digit3Key.wasPressedThisFrame) Answer(2);
        else if (kb.digit4Key.wasPressedThisFrame) Answer(3);
        else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Next();
    }
}
