using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Tracks the current spelling phase, word, and target letter. Progress changes are
/// synchronized through Photon RPCs so the participant and operator views stay aligned.
/// </summary>
public class SessionTracker : MonoBehaviour
{
    private PhotonView photonView;

    public TextMeshProUGUI logTMP;

    [SerializeField] TextMeshProUGUI phaseUIText;
    [SerializeField] TextMeshProUGUI currentWordUIText;
    [SerializeField] TextMeshProUGUI currentLetterUIText;
    [SerializeField] GameObject GameManagerTagType;  // Drag Player Stats GameObject here in the Inspector

    public delegate void WordChangeHandler();
    public event WordChangeHandler OnWordChanged;

    public delegate void LetterChangeHandler(char newLetter);
    public event LetterChangeHandler OnLetterChanged;

    public delegate void PhaseChangeHandler(int newPhase);
    public event PhaseChangeHandler OnPhaseChanged;

    public int currentPhase = 1;
    public int currentWord = 1;
    public int currentLetterIndex = 0; // Track the current letter index

    public string[][] wordsPerPhase = {
        new string[] {"BED", "FISH", "ROCK", "HOUSE", "PLANET"},
        new string[] {"DOG", "STAR", "LAMP", "TRAIN", "SILVER"},
        new string[] {"SUN", "WIND", "LEAF", "BEACH", "GARDEN"}
    };

    private string tempUserID = "TempUser";
    public string userID = "";           // remember to change to nothing when building    

    void Start()
    {
        photonView = GetComponent<PhotonView>();
        UpdateUI();
    }

    [PunRPC]
    public void UpdateUI()
    {
        if (GameManagerTagType == null || GameManagerTagType.CompareTag("User")) return;
        if (phaseUIText == null || currentWordUIText == null || currentLetterUIText == null) return;  // Don't update for the User as they don't have the UI visible

        Debug.Log($"Updating UI: Phase {currentPhase}, Word {GetCurrentWord()}, Letter {GetCurrentLetter()}");
        phaseUIText.text = currentPhase.ToString();
        currentWordUIText.text = GetCurrentWord();
        currentLetterUIText.text = TryGetCurrentLetter(out char letter) ? letter.ToString() : "";
    }

    public void OnUpdateUI()
    {
        PhotonView photonView = PhotonView.Get(this);
        photonView.RPC("UpdateUI", RpcTarget.All);
    }


    [PunRPC]
    public void UpdateNetworkSessionTrackers()
    {
       currentWord++;
       OnWordChanged?.Invoke(); // Notify listeners of new word
       currentLetterIndex = 0; // Reset for next word
    }

    public void OnUpdateNetworkSessionTrackers()
    {
        PhotonView photonView = PhotonView.Get(this);
        photonView.RPC("UpdateNetworkSessionTrackers", RpcTarget.All);
    }

    public void CompleteLetter()
    {
        string currentWord = GetCurrentWord();
        Debug.Log($"CompleteLetter called. CurrentWord: {currentWord}, CurrentLetterIndex: {currentLetterIndex}");

        if (GameManagerTagType.tag == "User")
        {
            if (currentLetterIndex < currentWord.Length - 1)
            {
                currentLetterIndex++; // Move to the next letter
                OnLetterChanged?.Invoke(GetCurrentLetter());
            }
            else
            {
                currentLetterIndex = 0; // Reset for next word
                CompleteWord();
            }
            OnUpdateUI();
        }
    }

    public void CompleteWord()
    {
        Debug.Log($"CompleteWord called. CurrentWord: {currentWord}");

        if (currentWord < wordsPerPhase[currentPhase - 1].Length)
        {

            OnUpdateNetworkSessionTrackers();
            //currentWord++;
            //OnWordChanged?.Invoke(); // Notify listeners of new word
        }
        else
        {
            OnCompletePhase();
        }
        OnUpdateUI();
    }

    [PunRPC]
    private void CompletePhase()
    {
        Debug.Log($"CompletePhase called. CurrentPhase: {currentPhase}");

        Debug.Log("wordsPerPhase.Length: " + wordsPerPhase.Length);

        if (currentPhase < wordsPerPhase.Length)
        {
            currentPhase++;
            currentWord = 1;  // Reset word for new phase
            currentLetterIndex = 0;
            OnPhaseChanged?.Invoke(currentPhase); // Notify listeners of new phase
            OnWordChanged?.Invoke();
        }
        else
        {
            Debug.Log("All phases completed!");
        }
        OnUpdateUI();
    }

    public void OnCompletePhase()
    {
        PhotonView photonView = PhotonView.Get(this);
        photonView.RPC("CompletePhase", RpcTarget.All);
    }



    [PunRPC]
    public void UI_ResetCurrentWordRPC()
    {
        currentLetterIndex = 0;
        UpdateUI();
        OnWordChanged?.Invoke();
    }

    [PunRPC]
    public void UI_ResetCurrentPhaseRPC()
    {
        currentWord = 1;
        currentLetterIndex = 0;
        UpdateUI();
        OnPhaseChanged?.Invoke(currentPhase);
    }

    [PunRPC]
    public void UI_NextWordRPC()
    {
        if (currentWord < wordsPerPhase[currentPhase - 1].Length)
        {
            currentWord++;
        }
        else if (currentPhase < wordsPerPhase.Length)
        {
            currentPhase++;  // Move to the next phase
            currentWord = 1; // Start from the first task of the new phase
        }
        currentLetterIndex = 0;  // Reset the letter index for new word
        UpdateUI();
        OnWordChanged?.Invoke();
        OnPhaseChanged?.Invoke(currentPhase);
    }

    [PunRPC]
    public void UI_PreviousWordRPC()
    {
        if (currentWord > 1)
        {
            currentWord--;
        }
        else if (currentPhase > 1)
        {
            currentPhase--;  // Move to the previous phase  
            currentWord = wordsPerPhase[currentPhase - 1].Length;  // Last task of the previous phase
        }

        currentLetterIndex = 0;
        UpdateUI();
        OnWordChanged?.Invoke();
        OnPhaseChanged?.Invoke(currentPhase);
    }

    public string GetUserID()
    {
        return userID;
    }

    public int GetCurrentPhase()
    {
        return currentPhase;
    }

    public string GetCurrentWord()
    {
        if (currentPhase > 0 && currentPhase <= wordsPerPhase.Length &&
            currentWord > 0 && currentWord <= wordsPerPhase[currentPhase - 1].Length)
        {
            return wordsPerPhase[currentPhase - 1][currentWord - 1];
        }
        else
        {
            Debug.LogError("Attempted to access an invalid word index.");
            return ""; // Return a default or empty string if out of bounds
        }
    }

    /// <summary>Returns false when the session is complete or its word/letter index is invalid.</summary>
    public bool TryGetCurrentLetter(out char letter)
    {
        string word = GetCurrentWord();
        if (string.IsNullOrEmpty(word) || currentLetterIndex < 0 || currentLetterIndex >= word.Length)
        {
            letter = default;
            return false;
        }
        letter = word[currentLetterIndex];
        return true;
    }

    public char GetCurrentLetter() => TryGetCurrentLetter(out char letter) ? letter : '\0';

    [PunRPC]
    public void ResetSessionRPC()
    {
        currentPhase = 1;
        currentWord = 1;
        currentLetterIndex = 0;
        UpdateUI();
    }

    public void OnButtonPress_ResetSession()
    {
        PhotonView photonView = PhotonView.Get(this);
        photonView.RPC("ResetSessionRPC", RpcTarget.All);
    }

    public void SetTempUserID(string LocaluserID)
    {
        tempUserID = LocaluserID;
    }

    [PunRPC]
    public void SetUserID()
    {
        if (GameManagerTagType.tag == "User")
        {
            userID = "Student";
        }
        else if (GameManagerTagType.tag == "Practitioner")
        {
            userID = "Practitioner";
        }
        else
        {
            userID = "Researcher"; // You are the researcher and you don't collect data
        }
        if (logTMP != null) logTMP.text += $"\nUser ID is set to {userID}";
    }

    public void OnSetUserIDsNetworkPressed()
    {
        PhotonView photonView = PhotonView.Get(this);
        photonView.RPC("SetUserID", RpcTarget.All);
    }
}


