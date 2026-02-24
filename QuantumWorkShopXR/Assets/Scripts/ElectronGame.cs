using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Element Quiz Game.
/// FIX: Much larger question and score text.
/// </summary>
public class ElectronGame : MonoBehaviour
{
    [Header("Quiz Settings")]
    public int questionsPerRound = 3;
    public float answerCooldown = 1.5f;

    [Header("Colors")]
    public Color neutralColor = new Color(0.3f, 0.4f, 0.6f);
    public Color correctColor = new Color(0.1f, 0.9f, 0.3f);
    public Color wrongColor = new Color(0.9f, 0.15f, 0.15f);
    public Color highlightColor = new Color(0.5f, 0.7f, 1.0f);

    private bool gameActive = false;
    private int targetZ = -1;
    private int currentQuestion = 0;
    private int score = 0;
    private int correctAnswerIndex = -1;
    private float lastAnswerTime = -10f;

    private GameObject quizRoot;
    private TextMeshPro questionTMP;
    private TextMeshPro scoreTMP;
    private GameObject[] answerBlocks = new GameObject[3];
    private TextMeshPro[] answerTexts = new TextMeshPro[3];
    private Renderer[] answerRenderers = new Renderer[3];
    private Material[] answerMaterials = new Material[3];

    private List<Transform> tips = new List<Transform>();
    private bool tipsFound = false;
    private int highlightedBlock = -1;

    private struct QuizQuestion
    {
        public string questionText;
        public string[] answers;
        public int correctIndex;
    }

    private List<QuizQuestion> questionPool = new List<QuizQuestion>();
    private AtomBuilder builder;

    public void StartGame(int atomicNumber)
    {
        builder = GetComponent<AtomBuilder>();
        targetZ = atomicNumber;
        currentQuestion = 0;
        score = 0;

        if (builder != null)
            builder.FillAllElectrons();

        GenerateQuestions();
        CreateQuizUI();
        ShowQuestion(0);

        gameActive = true;
        tipsFound = false;
        highlightedBlock = -1;

        Debug.Log("[Quiz] Started for Z=" + targetZ +
                  " " + AtomBuilder.Names[targetZ - 1] +
                  " | " + questionPool.Count + " questions");
    }

    void Update()
    {
        if (!gameActive) return;

        if (!tipsFound) { FindTips(); return; }

        for (int i = tips.Count - 1; i >= 0; i--)
        {
            if (!tips[i]) { tips.RemoveAt(i); if (tips.Count == 0) tipsFound = false; }
        }

        if (Time.time - lastAnswerTime < answerCooldown) return;

        int closest = -1;
        float closestDist = float.MaxValue;
        float highlightDist = 0.08f;
        float selectDist = 0.04f;

        foreach (var tip in tips)
        {
            if (!tip) continue;
            for (int b = 0; b < 3; b++)
            {
                if (!answerBlocks[b] || !answerBlocks[b].activeSelf) continue;
                float d = Vector3.Distance(tip.position, answerBlocks[b].transform.position);
                if (d < closestDist) { closestDist = d; closest = b; }
            }
        }

        if (closest >= 0 && closestDist < highlightDist)
        {
            SetHighlight(closest);
            if (closestDist < selectDist) OnAnswerSelected(closest);
        }
        else
        {
            ClearHighlight();
        }
    }

    // =============================================
    // QUESTION GENERATION
    // =============================================

    void GenerateQuestions()
    {
        questionPool.Clear();
        if (targetZ < 1 || targetZ > 20) return;

        int z = targetZ;
        string name = AtomBuilder.Names[z - 1];
        string sym = AtomBuilder.Sym[z - 1];
        int mass = AtomBuilder.Mass[z - 1];
        int[] shells = AtomBuilder.ShellConfigs[z - 1];
        int shellCount = shells.Length;
        int totalElectrons = z;

        List<int> types = new List<int> { 0, 1, 2, 3, 4, 5 };
        ShuffleList(types);

        for (int i = 0; i < Mathf.Min(questionsPerRound, types.Count); i++)
        {
            QuizQuestion q = new QuizQuestion();
            q.answers = new string[3];

            switch (types[i])
            {
                case 0:
                    q.questionText = "What is the atomic\nnumber of " + name + "?";
                    q.correctIndex = Random.Range(0, 3);
                    for (int a = 0; a < 3; a++)
                        q.answers[a] = (a == q.correctIndex) ? z.ToString() : GetWrongNumber(z, 1, 20).ToString();
                    break;

                case 1:
                    q.questionText = "How many electrons\ndoes " + name + " have?";
                    q.correctIndex = Random.Range(0, 3);
                    for (int a = 0; a < 3; a++)
                        q.answers[a] = (a == q.correctIndex) ? totalElectrons.ToString() : GetWrongNumber(totalElectrons, 1, 20).ToString();
                    break;

                case 2:
                    q.questionText = "What is the symbol\nfor " + name + "?";
                    q.correctIndex = Random.Range(0, 3);
                    for (int a = 0; a < 3; a++)
                        q.answers[a] = (a == q.correctIndex) ? sym : GetWrongSymbol(z);
                    break;

                case 3:
                    q.questionText = "How many electron\nshells does " + name + " have?";
                    q.correctIndex = Random.Range(0, 3);
                    for (int a = 0; a < 3; a++)
                        q.answers[a] = (a == q.correctIndex) ? shellCount.ToString() : GetWrongNumber(shellCount, 1, 4).ToString();
                    break;

                case 4:
                    q.questionText = "What is the mass\nnumber of " + name + "?";
                    q.correctIndex = Random.Range(0, 3);
                    for (int a = 0; a < 3; a++)
                        q.answers[a] = (a == q.correctIndex) ? mass.ToString() : GetWrongNumber(mass, 1, 45).ToString();
                    break;

                case 5:
                    int firstShell = shells[0];
                    q.questionText = "How many electrons\nin " + name + "'s first shell?";
                    q.correctIndex = Random.Range(0, 3);
                    for (int a = 0; a < 3; a++)
                        q.answers[a] = (a == q.correctIndex) ? firstShell.ToString() : GetWrongNumber(firstShell, 1, 8).ToString();
                    break;
            }

            EnsureUniqueAnswers(q);
            questionPool.Add(q);
        }
    }

    int GetWrongNumber(int correct, int min, int max)
    {
        int wrong;
        int attempts = 0;
        do
        {
            wrong = correct + Random.Range(-5, 6);
            if (wrong == correct) wrong = correct + (Random.Range(0, 2) == 0 ? 1 : -1);
            wrong = Mathf.Clamp(wrong, min, max);
            attempts++;
        } while (wrong == correct && attempts < 20);
        if (wrong == correct) wrong = Mathf.Clamp(correct + 1, min, max);
        return wrong;
    }

    string GetWrongSymbol(int correctZ)
    {
        int wrong;
        int attempts = 0;
        do { wrong = Random.Range(0, 20); attempts++; }
        while (wrong == correctZ - 1 && attempts < 20);
        return AtomBuilder.Sym[wrong];
    }

    void EnsureUniqueAnswers(QuizQuestion q)
    {
        for (int i = 0; i < 3; i++)
        {
            for (int j = i + 1; j < 3; j++)
            {
                if (q.answers[i] == q.answers[j])
                {
                    int toFix = (j == q.correctIndex) ? i : j;
                    if (toFix == q.correctIndex) toFix = (i == q.correctIndex) ? j : i;
                    int val;
                    if (int.TryParse(q.answers[toFix], out val))
                        q.answers[toFix] = (val + Random.Range(1, 4)).ToString();
                    else
                        q.answers[toFix] = AtomBuilder.Sym[Random.Range(0, 20)];
                }
            }
        }
    }

    void ShuffleList(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // =============================================
    // QUIZ UI - LARGE TEXT
    // =============================================

    void CreateQuizUI()
    {
        if (quizRoot) Destroy(quizRoot);
        quizRoot = new GameObject("QuizPanel");

        Transform cam = Camera.main ? Camera.main.transform : null;
        if (cam)
        {
            Vector3 camPos = cam.position;
            Vector3 right = cam.right; right.y = 0; right.Normalize();
            Vector3 fwd = cam.forward; fwd.y = 0; fwd.Normalize();

            Vector3 quizPos = camPos - right * 0.85f + fwd * 0.25f;
            quizPos.y = camPos.y - 0.05f;
            quizRoot.transform.position = quizPos;

            Vector3 toUser = camPos - quizPos;
            toUser.y = 0f;
            if (toUser.sqrMagnitude > 0.001f)
                quizRoot.transform.rotation = Quaternion.LookRotation(toUser.normalized, Vector3.up);
        }

        // =============================================
        // QUESTION TEXT - MUCH bigger (scale 0.05 instead of 0.018)
        // =============================================
        var qObj = new GameObject("QuestionText");
        qObj.transform.SetParent(quizRoot.transform, false);
        qObj.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        qObj.transform.localScale = Vector3.one * 0.045f;

        questionTMP = qObj.AddComponent<TextMeshPro>();
        questionTMP.text = "";
        questionTMP.fontSize = 7f;
        questionTMP.fontStyle = FontStyles.Bold;
        questionTMP.color = Color.white;
        questionTMP.alignment = TextAlignmentOptions.Center;
        questionTMP.enableWordWrapping = false;

        var qrt = qObj.GetComponent<RectTransform>();
        if (qrt) qrt.sizeDelta = new Vector2(20f, 6f);

        // =============================================
        // SCORE TEXT - Bigger (scale 0.035 instead of 0.012)
        // =============================================
        var sObj = new GameObject("ScoreText");
        sObj.transform.SetParent(quizRoot.transform, false);
        sObj.transform.localPosition = new Vector3(0f, 0.40f, 0f);
        sObj.transform.localScale = Vector3.one * 0.030f;

        scoreTMP = sObj.AddComponent<TextMeshPro>();
        scoreTMP.text = "Score: 0/" + questionsPerRound;
        scoreTMP.fontSize = 6f;
        scoreTMP.color = new Color(0.7f, 0.9f, 1f);
        scoreTMP.alignment = TextAlignmentOptions.Center;
        scoreTMP.enableWordWrapping = false;

        var srt = sObj.GetComponent<RectTransform>();
        if (srt) srt.sizeDelta = new Vector2(18f, 4f);

        // =============================================
        // 3 ANSWER BLOCKS - bigger blocks with bigger text
        // =============================================
        float blockSize = 0.12f;   // Was 0.10
        float blockGap = 0.04f;    // Was 0.03
        float totalWidth = 3 * blockSize + 2 * blockGap;
        float startX = -totalWidth / 2f + blockSize / 2f;

        for (int i = 0; i < 3; i++)
        {
            float x = startX + i * (blockSize + blockGap);

            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Answer_" + i;
            block.transform.SetParent(quizRoot.transform, false);
            block.transform.localPosition = new Vector3(x, 0f, 0f);
            block.transform.localScale = new Vector3(blockSize, blockSize, 0.03f);

            var rend = block.GetComponent<Renderer>();
            Material mat = MakeMat(neutralColor);
            rend.material = mat;
            answerMaterials[i] = mat;
            answerRenderers[i] = rend;

            var bc = block.GetComponent<BoxCollider>();
            if (bc) bc.isTrigger = true;

            answerBlocks[i] = block;

            // Answer text on -Z face (toward user)
            var tObj = new GameObject("AnswerText_" + i);
            tObj.transform.SetParent(block.transform, false);
            tObj.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            tObj.transform.localScale = Vector3.one * 0.45f;

            var tmp = tObj.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.fontSize = 10f;    // Was 8
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableWordWrapping = false;

            var rt = tObj.GetComponent<RectTransform>();
            if (rt) rt.sizeDelta = new Vector2(2f, 2f);

            answerTexts[i] = tmp;
        }

        Debug.Log("[Quiz] UI created at " + quizRoot.transform.position);
    }

    void ShowQuestion(int index)
    {
        if (index >= questionPool.Count) { EndQuiz(); return; }

        QuizQuestion q = questionPool[index];
        correctAnswerIndex = q.correctIndex;
        currentQuestion = index;

        if (questionTMP) questionTMP.text = q.questionText;

        float blockSize = 0.12f;
        for (int i = 0; i < 3; i++)
        {
            if (answerTexts[i]) answerTexts[i].text = q.answers[i];
            if (answerMaterials[i]) answerMaterials[i].color = neutralColor;
            if (answerBlocks[i])
            {
                answerBlocks[i].SetActive(true);
                answerBlocks[i].transform.localScale = new Vector3(blockSize, blockSize, 0.03f);
            }
        }

        if (scoreTMP) scoreTMP.text = "Score: " + score + "/" + questionsPerRound +
                                       "  |  Q" + (index + 1) + "/" + questionsPerRound;

        Debug.Log("[Quiz] Q" + (index + 1) + ": " + q.questionText.Replace("\n", " ") +
                  " | correct=" + q.answers[q.correctIndex]);
    }

    void OnAnswerSelected(int blockIndex)
    {
        lastAnswerTime = Time.time;
        bool isCorrect = (blockIndex == correctAnswerIndex);
        float blockSize = 0.12f;

        if (isCorrect)
        {
            score++;
            Debug.Log("[Quiz] CORRECT! Score=" + score);
            if (answerMaterials[blockIndex]) answerMaterials[blockIndex].color = correctColor;
            if (answerBlocks[blockIndex])
                answerBlocks[blockIndex].transform.localScale = new Vector3(blockSize * 1.2f, blockSize * 1.2f, 0.05f);
        }
        else
        {
            Debug.Log("[Quiz] WRONG! Selected " + blockIndex + " correct=" + correctAnswerIndex);
            if (answerMaterials[blockIndex]) answerMaterials[blockIndex].color = wrongColor;
            if (answerMaterials[correctAnswerIndex])
                answerMaterials[correctAnswerIndex].color = correctColor;
        }

        if (scoreTMP) scoreTMP.text = "Score: " + score + "/" + questionsPerRound +
                                       (isCorrect ? "  |  Correct!" : "  |  Wrong!");

        StartCoroutine(NextQuestionAfterDelay());
    }

    IEnumerator NextQuestionAfterDelay()
    {
        yield return new WaitForSeconds(answerCooldown);
        if (currentQuestion + 1 < questionPool.Count)
            ShowQuestion(currentQuestion + 1);
        else
            EndQuiz();
    }

    void EndQuiz()
    {
        gameActive = false;

        string resultText;
        Color resultColor;

        if (score == questionsPerRound)
        {
            resultText = "Perfect!\n" + score + "/" + questionsPerRound;
            resultColor = correctColor;
        }
        else if (score >= questionsPerRound / 2)
        {
            resultText = "Good Job!\n" + score + "/" + questionsPerRound;
            resultColor = new Color(1f, 0.8f, 0.2f);
        }
        else
        {
            resultText = "Try Again!\n" + score + "/" + questionsPerRound;
            resultColor = wrongColor;
        }

        if (questionTMP) { questionTMP.text = resultText; questionTMP.color = resultColor; }
        if (scoreTMP) scoreTMP.text = "Select another element\nto play again!";

        for (int i = 0; i < 3; i++)
            if (answerBlocks[i]) answerBlocks[i].SetActive(false);

        Debug.Log("[Quiz] Finished! Score=" + score + "/" + questionsPerRound);
    }

    // =============================================
    // HIGHLIGHT / INTERACTION
    // =============================================

    void SetHighlight(int index)
    {
        if (highlightedBlock == index) return;
        ClearHighlight();
        highlightedBlock = index;
        float blockSize = 0.12f;
        if (index >= 0 && index < 3 && answerMaterials[index] != null)
        {
            Color current = answerMaterials[index].color;
            if (current != correctColor && current != wrongColor)
            {
                answerMaterials[index].color = highlightColor;
                if (answerBlocks[index])
                    answerBlocks[index].transform.localScale = new Vector3(blockSize * 1.1f, blockSize * 1.1f, 0.04f);
            }
        }
    }

    void ClearHighlight()
    {
        float blockSize = 0.12f;
        if (highlightedBlock >= 0 && highlightedBlock < 3)
        {
            if (answerMaterials[highlightedBlock] != null)
            {
                Color current = answerMaterials[highlightedBlock].color;
                if (current != correctColor && current != wrongColor)
                {
                    answerMaterials[highlightedBlock].color = neutralColor;
                    if (answerBlocks[highlightedBlock])
                        answerBlocks[highlightedBlock].transform.localScale = new Vector3(blockSize, blockSize, 0.03f);
                }
            }
        }
        highlightedBlock = -1;
    }

    void FindTips()
    {
        tips.Clear();
        foreach (var o in FindObjectsOfType<GameObject>())
            if (o.activeInHierarchy && o.name == "XRHand_IndexTip")
                tips.Add(o.transform);
        if (tips.Count > 0)
        {
            tipsFound = true;
            Debug.Log("[Quiz] Found " + tips.Count + " fingertips");
        }
    }

    Material MakeMat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (!s) s = Shader.Find("Standard");
        if (!s) s = Shader.Find("Diffuse");
        Material m = new Material(s);
        m.color = c;
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.3f);
        }
        return m;
    }

    void OnDestroy()
    {
        if (quizRoot) Destroy(quizRoot);
    }
}

public class PulseGlow : MonoBehaviour
{
    Renderer rend;
    Color baseCol;
    void Start() { rend = GetComponent<Renderer>(); if (rend) baseCol = rend.material.color; }
    void Update()
    {
        if (!rend) return;
        float p = 1f + Mathf.Sin(Time.time * 3f) * 0.3f;
        Color c = baseCol * p; c.a = 1f; rend.material.color = c;
    }
}