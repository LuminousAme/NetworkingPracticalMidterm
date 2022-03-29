using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cube : MonoBehaviour
{
    [SerializeField] int cubeId = 0;
    public int GetCubeId() => cubeId;

    [SerializeField] float timeBetweenSends = 0.05f;
    float elapsedTime = 0f;
    Vector3 lastSent;
    bool shouldMove = true;

    private void OnEnable()
    {
        ChatManager.onStartType += TurnOffMovement;
        ChatManager.onEndType += TurnOnMovement;
    }

    private void OnDisable()
    {
        ChatManager.onStartType -= TurnOffMovement;
        ChatManager.onEndType -= TurnOnMovement;
    }

    // Start is called before the first frame update
    void Start()
    {
        elapsedTime = 0f;
        shouldMove = true;
        lastSent = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if(shouldMove)
        {
            if ((Client.instance != null && Client.instance.GetClientId() == cubeId) || Client.instance == null)
            {
                transform.position += new Vector3(Input.GetAxis("Horizontal") * Time.deltaTime * 2f,
                    0, Input.GetAxis("Vertical") * Time.deltaTime * 2f);
            }
        }

        if(Client.instance != null && Client.instance.GetClientId() == cubeId)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= timeBetweenSends)
            {
                if (lastSent != transform.position)
                {
                    Client.instance.SendPosUpdate(transform.position);
                    lastSent = transform.position;
                }
                elapsedTime = 0f;
            }
        }
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
        Debug.Log("Pos Recieved: " + pos);
    }

    void TurnOffMovement()
    {
        shouldMove = false;
    }

    void TurnOnMovement()
    {
        shouldMove = true;
    }
}
