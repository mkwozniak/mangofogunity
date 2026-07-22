using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MangoFog;

[RequireComponent(typeof(Rigidbody2D))]
public class MangoFog2DController : MonoBehaviour
{
    public float moveSpeed;
    Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;
    public MangoFogUnit fogUnit;
    public List<Sprite> charSprites = new List<Sprite>();

    protected void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        fogUnit.transform.rotation = Quaternion.Euler(new Vector3(0, 0, -180));
    }

    protected void Update()
    {

        if (Input.GetKey(KeyCode.W))
        {
            rb.velocity = Vector3.up * moveSpeed;
            fogUnit.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            if (charSprites[0])
                spriteRenderer.sprite = charSprites[0];
        }
        else if (Input.GetKey(KeyCode.S))
        {
            rb.velocity = -Vector3.up * moveSpeed;
            fogUnit.transform.rotation = Quaternion.Euler(new Vector3(0, 0, -180));
            if (charSprites[1])
                spriteRenderer.sprite = charSprites[1];
        }
        if (Input.GetKey(KeyCode.A))
        {
            rb.velocity = -Vector3.right * moveSpeed;
            fogUnit.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 90));
            if (charSprites[2])
                spriteRenderer.sprite = charSprites[2];
        }
        else if (Input.GetKey(KeyCode.D))
        {
            rb.velocity = Vector3.right * moveSpeed;
            fogUnit.transform.rotation = Quaternion.Euler(new Vector3(0, 0, -90));
            if (charSprites[3])
                spriteRenderer.sprite = charSprites[3];
        }
    }
}
