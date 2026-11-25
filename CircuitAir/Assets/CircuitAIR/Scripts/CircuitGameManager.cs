using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Circuit AIR match: start, elapsed time, errors and result display.
/// Add this component to one GameObject in the main scene and connect its UI fields.
/// </summary>
public class CircuitGameManager : MonoBehaviour
{
    public static CircuitGameManager Instance { get; private set; }

    [Header("HUD")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text errorsText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject resultsPanel;

    [Header("Results")]
    [SerializeField] private TMP_Text finalTimeText;
    [SerializeField] private TMP_Text finalErrorsText;

    [Header("Game rules")]
    [Tooltip("Avoids counting multiple overlapping wall colliders as separate errors in the same instant.")]
    [SerializeField, Min(0f)] private float errorCooldownSeconds = 0.25f;

    private float elapsedSeconds;
    private float lastErrorTime = -100f;
    private int errors;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public int Errors => errors;
    public float ElapsedSeconds => elapsedSeconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Há mais de um CircuitGameManager na cena. Mantenha apenas um.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartChallenge);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartChallenge);

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (hudPanel != null)
            hudPanel.SetActive(true);

        UpdateHud();
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        elapsedSeconds += Time.deltaTime;
        if (timerText != null)
            timerText.text = FormatTime(elapsedSeconds);
    }

    /// <summary>Connect this method to the Start button, or let the script connect it automatically.</summary>
    public void StartChallenge()
    {
        elapsedSeconds = 0f;
        errors = 0;
        lastErrorTime = -100f;
        isPlaying = true;

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (hudPanel != null)
            hudPanel.SetActive(true);

        if (startButton != null)
            startButton.gameObject.SetActive(false);

        if (restartButton != null)
            restartButton.gameObject.SetActive(false);

        UpdateHud();
    }

    /// <summary>Resets the counters and immediately starts a new attempt.</summary>
    public void RestartChallenge()
    {
        StartChallenge();
    }

    /// <summary>Called by a CircuitZone when the tracked magnifying-glass tip hits a wall.</summary>
    public void RegisterError()
    {
        if (!isPlaying)
            return;

        if (Time.unscaledTime - lastErrorTime < errorCooldownSeconds)
            return;

        lastErrorTime = Time.unscaledTime;
        errors++;
        UpdateHud();
    }

    /// <summary>Called when the tracked magnifying-glass tip reaches the finish zone.</summary>
    public void FinishChallenge()
    {
        if (!isPlaying)
            return;

        isPlaying = false;

        if (finalTimeText != null)
            finalTimeText.text = FormatTime(elapsedSeconds);

        if (finalErrorsText != null)
            finalErrorsText.text = errors.ToString();

        if (hudPanel != null)
            hudPanel.SetActive(false);

        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        if (restartButton != null)
            restartButton.gameObject.SetActive(true);
    }

    private void UpdateHud()
    {
        if (timerText != null)
            timerText.text = FormatTime(elapsedSeconds);

        if (errorsText != null)
            errorsText.text = errors.ToString();
    }

    private static string FormatTime(float timeInSeconds)
    {
        int totalHundredths = Mathf.Max(0, Mathf.FloorToInt(timeInSeconds * 100f));
        int minutes = totalHundredths / 6000;
        int seconds = (totalHundredths / 100) % 60;
        int hundredths = totalHundredths % 100;
        return $"{minutes:00}:{seconds:00}.{hundredths:00}";
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(StartChallenge);

        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartChallenge);

        if (Instance == this)
            Instance = null;
    }
}
