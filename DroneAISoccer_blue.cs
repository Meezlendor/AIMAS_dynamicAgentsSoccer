using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Panda;


[RequireComponent(typeof(DroneController))]
public class DroneAISoccer_blue : MonoBehaviour
{
    private DroneController m_Drone; // the drone controller we want to use

    public GameObject terrain_manager_game_object;
    TerrainManager terrain_manager;

    public GameObject[] friends;
    public string friend_tag;
    public GameObject[] enemies;
    public string enemy_tag;

    public GameObject own_goal;
    public GameObject other_goal;
    public GameObject ball;

    private Vector3 oldBallPlace;

    PandaBehaviour pandaBehaviour;


    private void Start()
    {
        // get the car controller
        m_Drone = GetComponent<DroneController>();
        terrain_manager = terrain_manager_game_object.GetComponent<TerrainManager>();
        pandaBehaviour = GetComponent<PandaBehaviour>();

        // note that both arrays will have holes when objects are destroyed
        // but for initial planning they should work
        friend_tag = gameObject.tag;
        if (friend_tag == "Blue")
            enemy_tag = "Red";
        else
            enemy_tag = "Blue";

        friends = GameObject.FindGameObjectsWithTag(friend_tag);
        enemies = GameObject.FindGameObjectsWithTag(enemy_tag);

        ball = GameObject.FindGameObjectWithTag("Ball");
        oldBallPlace = ball.transform.position;

    }


    private void Update()
    {
        pandaBehaviour.Reset();
        pandaBehaviour.Tick();
        oldBallPlace = ball.transform.position;
        Color myColor = friend_tag == "Blue" ? Color.blue : Color.red;
        foreach(var friend in friends)
        {
            Debug.DrawLine(transform.position, friend.transform.position, myColor);
        }
        Debug.DrawLine(transform.position, ball.transform.position, myColor);
    }

    private void GoToPos(Vector3 pos)
    {
        Vector3 deltaPos = pos - transform.position;
        if (Vector3.Dot(m_Drone.velocity.normalized, deltaPos.normalized) < 0.5)
            deltaPos -= m_Drone.velocity;
        m_Drone.Move(deltaPos.x, deltaPos.z);
    }

    //Goalie tree


    [Task]
    void InterpolateBall()
    {
        Vector3 normalVectorToGoal = (other_goal.transform.position - own_goal.transform.position).normalized;
        Vector3 speedOfBall = ball.transform.position - oldBallPlace;
        if (Vector3.Dot(normalVectorToGoal, speedOfBall.normalized) < 0)
        {
            //there's a risk the ball enters our cage, prevent it if in reasonable area
            //but don't move if you're already almost touching the ball
            if (Vector3.Distance(ball.transform.position, transform.position) < 5)
                return;
            float xPos = own_goal.transform.position.x + normalVectorToGoal.x * 5;
            float gammaFactor = (xPos - ball.transform.position.x) / speedOfBall.x;
            float predictedZPos = ball.transform.position.z + gammaFactor * speedOfBall.z;
            predictedZPos = Math.Min(own_goal.transform.position.z + 20, predictedZPos);
            predictedZPos = Math.Max(own_goal.transform.position.z - 20, predictedZPos);
            Debug.DrawLine(transform.position, new Vector3(xPos, 0, predictedZPos), Color.black);
            Debug.DrawLine(transform.position, transform.position + 5 * normalVectorToGoal, Color.yellow);
            GoToPos(new Vector3(xPos, 0, predictedZPos));
        }
    }

    [Task]
    bool NoGoalie()
    {
        bool presence = false;
        foreach(var friend in friends)
        {
            presence = presence || Vector3.Distance(own_goal.transform.position, friend.transform.position) < 20;
        }
        return !presence;
    }

    [Task]
    bool IsGoalie()
    {
        var myDistance = Vector3.Distance(own_goal.transform.position, transform.position);
        return AmClosestToGoal() && myDistance<=20;
    }

    [Task]
    bool AmClosestToGoal()
    {
        var myDistance = Vector3.Distance(own_goal.transform.position, transform.position);
        bool amClosest = true;
        foreach (var friend in friends)
        {
            amClosest = amClosest && (Vector3.Distance(friend.transform.position, own_goal.transform.position) >= myDistance);
        }
        return amClosest;
    }

    [Task]
    void GoGoalie()
    {
        GoToPos(own_goal.transform.position);
    }

    // Attack tree

    [Task]
    bool CanAttack()
    {
        bool ballInMySide = Vector3.Distance(own_goal.transform.position, ball.transform.position) < Vector3.Distance(other_goal.transform.position, ball.transform.position);
        //return !ballInMySide;
        if (ballInMySide)
        {
            if (Vector3.Distance(own_goal.transform.position, ball.transform.position) < 20)
                return false;//please do not attack when you're on the verge of being owned by the opponent
            float friendDistance = float.MaxValue;
            bool friendClosest = true;
            foreach(var friend in friends)
            {
                float distance = Vector3.Distance(friend.transform.position, ball.transform.position);
                if (distance < friendDistance)
                    friendDistance = distance;
            }
            foreach(var enemy in enemies)
            {
                if (Vector3.Distance(enemy.transform.position, ball.transform.position) < friendDistance+5)
                    friendClosest = false;
            }
            return friendClosest;
        } else
        {
            float myDistance = Vector3.Distance(transform.position, ball.transform.position);
            foreach(var friend in friends)
            {
                myDistance = Math.Min(Vector3.Distance(friend.transform.position, ball.transform.position), myDistance);
            }
            int closerEnemies = 0;
            foreach(var enemy in enemies)
            {
                if (Vector3.Distance(enemy.transform.position, ball.transform.position) < myDistance)
                    closerEnemies++;
            }
            return closerEnemies < 2;
        }
    }

    [Task]
    bool IsAligned()
    {
        if (Vector3.Distance(ball.transform.position, transform.position) > 20)
            return false;
        Vector3 axis = (ball.transform.position - transform.position).normalized;
        //yet, can shoot backwards
        foreach(var friend in friends)
        {
            Vector3 axisFriend=(friend.transform.position-transform.position).normalized;
            if (Vector3.Dot(axisFriend, axis) > 0.85)
                return true;
        }
        Vector3 axisGoal= (other_goal.transform.position - transform.position).normalized;
        if (Vector3.Dot(axisGoal, axis) > 0.7)
            return true;
        return false;
    }

    [Task]
    void Shoot()
    {
        //FIXME : currently, it just rushes the boal
        GoToPos(ball.transform.position);
    }

    [Task]
    void PlaceWell()
    {
        //place yourself ideally behind the ball along the axis from the ball to the target
        Vector3 targetPos=other_goal.transform.position;

        Vector3 axis = (Vector3.ProjectOnPlane(ball.transform.position,Vector3.up) - targetPos).normalized;

        //check that we're not going opposite to this axis
        Vector3 myAxisToBall = (ball.transform.position - transform.position).normalized;
        if (Vector3.Dot(axis, myAxisToBall) < -0.8)
        {
            //There's a chance to hit the ball, so go to the side of the ball (sideways is towards the exterior of the terrain)
            Vector3 sideAxis;
            if (ball.transform.position.z > (terrain_manager.myInfo.z_high - terrain_manager.myInfo.z_low) / 2)
                sideAxis = -Vector3.forward;
            else
                sideAxis = Vector3.forward;
            GoToPos(ball.transform.position + 5 * sideAxis);
        } else
            GoToPos(ball.transform.position + 5 * axis);
    }

    [Task]
    void GoOldFar()
    {
        int iStart, iEnd;
        Vector3 bestPos = transform.position;
        if (other_goal.transform.position.x < own_goal.transform.position.x)
        {
            iStart = 2;
            iEnd = terrain_manager.myInfo.x_N / 2;
            foreach(var friend in friends)
            {
                if (friend.transform != transform)
                {
                    iEnd = Math.Min(iEnd, terrain_manager.myInfo.get_i_index(friend.transform.position.x));
                }
            }
        } else
        {
            iStart = terrain_manager.myInfo.x_N / 2;
            iEnd = terrain_manager.myInfo.x_N-2;
            foreach (var friend in friends)
            {
                if (friend.transform != transform)
                {
                    iStart = Math.Max(iStart, terrain_manager.myInfo.get_i_index(friend.transform.position.x));
                }
            }
        }
        float bestDistance = 0;
        Debug.Log($"Going far i in [{iStart},{iEnd}], j in [0, {terrain_manager.myInfo.z_N}], ({friend_tag})");
        for(int i=iStart; i < iEnd; i++)
        {
            for(int j=2; j < terrain_manager.myInfo.z_N-2;j++)
            {
                if (terrain_manager.myInfo.traversability[i, j] == 0)
                {
                    Vector3 pos = new Vector3(terrain_manager.myInfo.get_x_pos(i), 0f, terrain_manager.myInfo.get_z_pos(j));
                    float localDist = 0;
                    foreach(var enemy in enemies)
                    {
                        localDist += Vector3.Distance(pos, enemy.transform.position);
                    }
                    if (localDist > bestDistance)
                    {
                        bestDistance = localDist;
                        bestPos = pos;
                    }
                }
            }
        }
        Debug.DrawLine(transform.position, bestPos, Color.yellow);
        GoToPos(bestPos);
    }

    [Task]
    void GoFar()
    {
        //Go beyond the guy that is pushing the ball, half distance from goal
        float distanceToBall = float.MaxValue;
        Vector3 axis = Vector3.zero;
        foreach (var friend in friends)
        {
            if (Vector3.Distance(friend.transform.position, ball.transform.position) < distanceToBall)
            {
                distanceToBall = Vector3.Distance(friend.transform.position, ball.transform.position);
                axis = (ball.transform.position - friend.transform.position).normalized;
            }
        }
        float distanceToGoal = Vector3.Distance(ball.transform.position, other_goal.transform.position);
        GoToPos(ball.transform.position + distanceToGoal * axis / 2);
    }

    //defend tree

    [Task]
    bool ClosestToBall()
    {
        float myDistance = Vector3.Distance(m_Drone.transform.position, ball.transform.position);
        foreach (var friend in friends)
        {
            if (Vector3.Distance(friend.transform.position, ball.transform.position) < myDistance)
                return false;
        }
        return true;
    }


    [Task]
    void StickCloseToEnemies()
    {
        Vector3 gravityCenter = ball.transform.position;
        int factor = 1;
        foreach(var enemy in enemies)
        {
            if (Vector3.Distance(enemy.transform.position, ball.transform.position) < 20)
            {
                gravityCenter += enemy.transform.position;
                factor ++;
            }
        }
        gravityCenter /= factor;
        GoToPos(gravityCenter);

    }

    [Task]
    void ProtectGoalie()
    {
        Vector3 protectionPlace = (ball.transform.position + own_goal.transform.position) / 2;
        GoToPos(protectionPlace);
    }
}
