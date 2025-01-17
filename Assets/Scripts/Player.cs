﻿using MoreMountains.Feedbacks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Controller2D))]
public class Player : MonoBehaviour
{

    Controller2D controller;
    Animator anim;

    public Vector2 velocity;
    public LayerMask traps;

    float StandardGravity = -0.5625f * 64;

    public static float gravity = -0.5625f * 64;
    float maxFallSpeed = 18.75f;// 10;
    float maxSpeed = 11.25f;
    float jumpForce = 15.5f;
    float acceleration = .375f * 64;
    float deacceleration = .46875f * 64;

    [SerializeField]
    public float power = 0;
    [SerializeField]
    float timeToFillPower = 2;

    private float maxPower => 100;
    [SerializeField]
    Image powerBar;

    public bool grounded;


    public bool gravityLock = false;

    public MMFeedbacks gravityChangeFeedbacks;
    public MMFeedbacks usePowersFeedbacks;
    public MMFeedbacks chargePowerFeedback;
    public MMFeedbacks onDeathFeedback;

    private float airTime;

    public List<int> keyIDs = new List<int>();
    private bool isDead;
    private bool CanJump => coyoteTime < .03f;
    public float coyoteTime;

    private Vector3 startPosition;
    private ITriggerable lastTriggerTriggered;
    List<ITriggerable> triggersThisRound = new List<ITriggerable>();
    List<ITriggerable> triggersLastRound = new List<ITriggerable>();

    void Start()
    {
        controller = GetComponent<Controller2D>();
        anim = GetComponent<Animator>();

        startPosition = transform.position;
        LevelManager.Instance.restartLevelDelegate += ResetPlayer;
        LevelManager.Instance.levelClearedDelegate += LevelCleared;

    }

    private void OnDisable()
    {
        LevelManager.Instance.restartLevelDelegate -= ResetPlayer;
        LevelManager.Instance.levelClearedDelegate -= LevelCleared;
    }

    private void LevelCleared()
    {
        gameObject.SetActive(false);
    }

    private void ResetPlayer()
    {
        transform.position = startPosition;
        power = 0;
        gravity = StandardGravity;
        isDead = false;
        velocity = new Vector2();
        Time.timeScale = 1;
        keyIDs.Clear();
    }

    private void OnEnable()
    {

        Shader.SetGlobalVector("PlayerPosition", transform.position);
    }

    void Update()
    {

        Shader.SetGlobalVector("PlayerPosition", transform.position);


        if (isDead)
            return;

        if (Input.GetKeyDown(KeyCode.O))
            power = 100;

        FindProximityObjects();

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (Input.GetKeyDown(KeyCode.LeftControl))
            if (TryToInteract())
                return;

        if (Input.GetKeyDown(KeyCode.Space) && !gravityLock)
        {
            FlipGravity();
        }

        if (controller.collisions.above || controller.collisions.below)
        {
            velocity.y = 0;
        }
        if (controller.collisions.left || controller.collisions.right)
        {
            velocity.x = 0;
        }

        grounded = (gravity > 0) ? controller.collisions.above : controller.collisions.below;
        if (grounded)
            coyoteTime = 0;
        
        else
            coyoteTime += Time.deltaTime;
        anim.SetBool("Grounded", grounded);

        //DEBUG
        if(CanJump)
            Debug.DrawLine(GetComponent<Collider2D>().bounds.min, GetComponent<Collider2D>().bounds.min + Vector3.up * .1f, Color.white, 10);
        //ENDDEBUG


        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            if (CanJump)
                Jump();
            else if (power >= 10)
            {
                InAirJump(input);
            }
        if ((Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) && power >= 10)
        {
            velocity.x = 0;
            velocity.y = Mathf.Sign(gravity) * maxFallSpeed * 2;
            power -= 10;
                usePowersFeedbacks.PlayFeedbacks();
        }


        if ((Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyUp(KeyCode.W)) && Mathf.Sign(gravity) != Mathf.Sign(velocity.y))
            velocity.y *= .5f;

        if (input.x != 0)
        {
            //if change direction, deaccelerate
            if (Mathf.Sign(input.x) != Mathf.Sign(velocity.x) && velocity.x != 0)
            {
                if (grounded)
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, deacceleration * Time.deltaTime);
                else
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, deacceleration * Time.deltaTime * .6f);
            }
            else //run, accelerate
            {
                if (grounded || airTime < .85f)
                {
                    if (Mathf.Abs(velocity.x) < maxSpeed)
                        velocity.x += acceleration * input.x * Time.deltaTime;
                    else if (Mathf.Sign(input.x) == Mathf.Sign(velocity.x))
                    {
                        velocity.x = input.x * maxSpeed;
                        if (grounded)
                            ChargePower();
                    }
                    
                }
                else 
                {
                    if (Mathf.Abs(velocity.x) < maxSpeed * .8f)
                        velocity.x += acceleration * input.x * Time.deltaTime * .5f;
                    else if (Mathf.Sign(input.x) == Mathf.Sign(velocity.x))
                    {
                        velocity.x = Mathf.MoveTowards(velocity.x, input.x * maxSpeed * .8f, deacceleration * Time.deltaTime * .1f);
                    }

                }


                
            }
        }
        else
        {
            if (grounded)
                velocity.x = Mathf.MoveTowards(velocity.x, 0, deacceleration * Time.deltaTime * .8f);
            else
                velocity.x = Mathf.MoveTowards(velocity.x, 0, deacceleration * Time.deltaTime * .4f);
        }

        velocity.y = Mathf.MoveTowards(velocity.y, maxFallSpeed * Mathf.Sign(gravity), Mathf.Abs(gravity) * Time.deltaTime);
        //velocity.y += gravity * Time.deltaTime;
       // if (Math.Abs(velocity.y) > maxFallSpeed)
       //     velocity.y = velocity.y.Sign() * maxFallSpeed;

        controller.Move(velocity * Time.deltaTime);

        anim.SetInteger("InputX", (int)input.x);

        if (input.x != 0)
            GetComponent<SpriteRenderer>().flipX = input.x > 0 ? false : true;

        GetComponent<SpriteRenderer>().flipY = Mathf.Sign(gravity) < 0 ? false : true;

        if (Input.GetKeyDown(KeyCode.R))
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        if (Physics2D.OverlapArea(transform.position - controller.col.bounds.extents * .8f, transform.position + controller.col.bounds.extents * .8f, traps))
        {
            DoDamage(); //todo: move to trap class when created IE Create trap class
        }
        if(powerBar)
            powerBar.rectTransform.sizeDelta = new Vector2(power, 50);
        if (grounded && Mathf.Abs(velocity.x) >= maxSpeed * .9f)
            airTime = 0;
        else
            airTime += Time.deltaTime;

        FindTriggers();
    }

    private void InAirJump(Vector2 input)
    {
        velocity.y = jumpForce * Mathf.Sign(-gravity);
        velocity.x = input.x * maxSpeed;
        power -= 10;
        usePowersFeedbacks.PlayFeedbacks();
    }

    private void Jump()
    {
        if (Mathf.Abs(velocity.x) < maxSpeed)
            velocity.y = jumpForce * Mathf.Sign(-gravity);
        else
            velocity.y = jumpForce * Mathf.Sign(-gravity);
    }

    private void ChargePower()
    {
        power = Mathf.Clamp(power + Time.deltaTime * Mathf.Abs(velocity.x), 0, maxPower);

    }

    public void ChargePowerAmount(float amount)
    {
        power = Mathf.Clamp(power + amount, 0, maxPower);
        chargePowerFeedback.PlayFeedbacks();
            
    }

    private void FindTriggers()
    {
        var nearbyColliders = Physics2D.OverlapBoxAll(transform.position, controller.col.bounds.size, 0);


        foreach (var trigger in nearbyColliders)
        {
            var triggerable = trigger.GetComponent<ITriggerable>();
            if (triggerable == null)
                continue;

            if (!triggersLastRound.Contains(triggerable))
            {
                triggerable.OnTriggerArrive(this);
            }
            triggerable.OnTrigger(this);

            triggersThisRound.Add(triggerable);
        }
        foreach (var trigger in triggersLastRound)
        {
            if (!triggersThisRound.Contains(trigger))
            {
                trigger.OnTriggerLeave(this);

            }
        }

        triggersLastRound = triggersThisRound;
        triggersThisRound = new List<ITriggerable>();
    }

    private void FindProximityObjects()
    {
        foreach (var item in Physics2D.OverlapCircleAll(transform.position, 2))
        {
            var gate = item.GetComponent<LockedGate>();
            if (gate && keyIDs.Contains(gate.gateLockID))
            {
                gate.Unlock();
            }
        }
    }

    private bool TryToInteract()
    {
        var cols = Physics2D.OverlapBoxAll(transform.position, Vector2.one * .5f, 0);
        foreach (var col in cols)
        {
            var interactable = col.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.OnInteract(controller);
                return true;
            }
        }
        return false;
    }

    private void FlipGravity()
    {
        gravityChangeFeedbacks.PlayFeedbacks();
        gravity *= -1;
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(0, 0, 150, 50), "air time: " + airTime);
    }

    public void DoDamage()
    {
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        onDeathFeedback.PlayFeedbacks();
        isDead = true;
        yield return new WaitForSecondsRealtime(.5f);
        LevelManager.Instance.RestartLevel();
    }

}
