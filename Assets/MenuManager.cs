using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [SerializeField] TMP_InputField ipInputField;

    public void OnJoinButtonPressed()
    {
        if(Client.instance != null)
        {
            Client.instance.ipAddress = ipInputField.text;
            Client.instance.StartClient();
            SceneManager.LoadScene(1);
        }
    }
}
