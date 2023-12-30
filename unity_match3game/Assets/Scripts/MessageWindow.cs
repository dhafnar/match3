using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// this is a UI component that can show a message, icon and button
[RequireComponent(typeof(RectXformMover))]
public class MessageWindow : MonoBehaviour
{
    public Image messageImage;
    public Text messageText;
    public Text buttonText;

    public Button button;

    public GameObject questionPortion;

    // sprite for losers
    public Sprite loseIcon;

    // sprite for winners
    public Sprite winIcon;

    // sprite for the level goal
    public Sprite goalIcon;

    public Sprite collectIcon;
    public Sprite timerIcon;
    public Sprite movesIcon;

    public Sprite goalCompleteIcon;
    public Sprite goalFailedIcon;

    public Image goalImage;
    public Text goalText;

    public GameObject collectionGoalLayout;

    Vector2 originalAnchoredPosition;


    private void ShowMessage(Sprite sprite = null, string message = "", string buttonMsg = "start",
        bool questions = false)
    {
        if (questions)
        {
            // Here we ask the user how they feel about the level they just completed
            Debug.Log("Questions = true");
            button.gameObject.SetActive(false);
            questionPortion.gameObject.SetActive(true);
        }
        else
        {
            // We don't show the questions, just the button to continue
            questionPortion.gameObject.SetActive(false);
            button.gameObject.SetActive(true);

            if (buttonText != null)
            {
                buttonText.text = buttonMsg;
            }
        }

        if (messageImage != null)
        {
            messageImage.sprite = sprite;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    public void ShowInstructionsMessage()
    {
        ShowMessage(winIcon, "How to play", "ok");
    }

    public void ShowScoreMessage(int scoreGoal)
    {
        string message = "score goal \n" + scoreGoal.ToString();
        ShowMessage(goalIcon, message, "start");
    }

    public void ShowWinMessage()
    {
        ShowMessage(winIcon, "level\ncomplete", "ok", true);
    }

    public void ShowLoseMessage()
    {
        ShowMessage(loseIcon, "level\nfailed", "ok", true);
    }

    public void ShowEndGameMessage()
    {
        ShowMessage(winIcon, "game completed", "exit");
    }

    public void ShowGoal(string caption = "", Sprite icon = null)
    {
        questionPortion.gameObject.SetActive(false);

        if (caption != "")
        {
            ShowGoalCaption(caption);
        }

        if (icon != null)
        {
            ShowGoalImage(icon);
        }
    }

    public void ShowGoalCaption(string caption = "", int offsetX = 0, int offsetY = 0)
    {
        if (goalText != null)
        {
            goalText.text = caption;
            RectTransform rectXform = goalText.GetComponent<RectTransform>();

            // Store the original anchoredPosition of the RectTransform
            if (originalAnchoredPosition == Vector2.zero)
            {
                originalAnchoredPosition = rectXform.anchoredPosition;
            }

            // Reset the anchoredPosition before applying the offset
            rectXform.anchoredPosition = originalAnchoredPosition;

            rectXform.anchoredPosition += new Vector2(offsetX, offsetY);
        }
    }

    public void ShowGoalImage(Sprite icon = null)
    {
        if (goalImage != null)
        {
            goalImage.gameObject.SetActive(true);
            goalImage.sprite = icon;
        }

        if (icon == null)
        {
            goalImage.gameObject.SetActive(false);
        }
    }


    public void ShowTimedGoal(int time)
    {
        string caption = time.ToString() + " seconds";
        ShowGoal(caption, timerIcon);
    }

    public void ShowMovesGoal(int moves)
    {
        string caption = moves.ToString() + " moves";
        ShowGoal(caption, movesIcon);
    }

    public void ShowCollectionGoal(bool state = true)
    {
        if (collectionGoalLayout != null)
        {
            collectionGoalLayout.SetActive(state);
        }

        if (state)
        {
            ShowGoal("", collectIcon);
        }
    }

    public void HideWindow()
    {
        // Hide the window
        this.gameObject.SetActive(false);
    }
}