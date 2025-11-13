using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public enum FoxState { 대기, 정찰, 경고, 추적, 공격, 스턴, 수면 }

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class FoxCtrl: Photon.MonoBehaviour
{
    GameObject[] cells;
    [SerializeField] private GameObject[] cellPrefabs;

    [Header("참조 변수")]
    public Transform traceTarget;
    [SerializeField] private Transform playerTarget;
    public Transform myTr;
    [SerializeField] private GameObject[] players;
    [SerializeField] private NavMeshAgent myTraceAgent;
    public Animator anim;
    public FoxState state = FoxState.대기;

    [Space(30)]
    [Range(0, 1000)] public int hp = 100;
    [Range(5f, 10f)] public float speed = 7.5f;

    [Header("밑에 있는 변수 일 수록 거리가 줄어야함.")]
    [SerializeField] float findDist = 10f;   // 발견
    [SerializeField] float traceDist = 8f;   // 추적
    [SerializeField] float attackDist = 2f;  // 공격
    [SerializeField] float roamingTimeSet = 10f;

    [SerializeField] float stunTime = 3f;

    [SerializeField] private bool traceAttack;
    [SerializeField] private bool isDie;
    [SerializeField] private bool isStun;
    [SerializeField] private bool hungry;
    [SerializeField] private bool sleep;
    [SerializeField] private bool roamingPause;

    [Header("시간 변수")]
    [SerializeField] private float attackDelayTime = 1.5f;
    private float attackCoolDown;

    Rigidbody myRbody;
    Vector3 currPos = Vector3.zero;
    Quaternion currRot = Quaternion.identity;

    // 네트워크 변수
    [Header("네트워크 변수")]
    PhotonView pv;
    [SerializeField] int net_Anim = 0;

    void Awake()
    {
        // 안전 초기화 (인스펙터 미할당 대비)
        if (!myTr) myTr = transform;
        if (!anim) anim = GetComponent<Animator>();
        if (!myRbody) myRbody = GetComponent<Rigidbody>();
        if (!myTraceAgent) myTraceAgent = GetComponent<NavMeshAgent>();
        if (!pv) pv = GetComponent<PhotonView>();

        if (!PhotonNetwork.isMasterClient)
        {
            if (myRbody)
                myRbody.isKinematic = true;

            if (myTraceAgent)
            {
                myTraceAgent.enabled = false;
                myTraceAgent.autoBraking = true;
                myTraceAgent.updateRotation = true;
            }
        }

        // 원격 플레이어의 위치 및 회전 값을 처리할 변수의 초기값 설정 
        pv.ObservedComponents[0] = this;
        pv.synchronization = ViewSynchronization.UnreliableOnChange;
        currPos = myTr.position;
        currRot = myTr.rotation;

    }

    void OnDisable()
    {
        Debug.Log("FoxCtrl Disabled: " + PhotonNetwork.isMasterClient);
        OnEnable();
    }

    void OnEnable()
    {
    }
    IEnumerator Start()
    {
        state = FoxState.대기;

        // 서버/마스터 판단 자리 (예시로 항상 실행)
        if (PhotonNetwork.isMasterClient)
        {
            StartCoroutine(ModeSet());
            StartCoroutine(ModeAction());
            StartCoroutine(TargetSetting());
        }
        yield return null;
    }

    void Update()
    {
        // 공격 지연 시간
        attackCoolDown += Time.deltaTime;
        if (attackCoolDown > attackDelayTime && state == FoxState.공격)
        {
            Attack();
            attackCoolDown = 0;
        }

        StunCool();


        if (PhotonNetwork.isMasterClient)
        {
            if (this.gameObject.GetComponent<FoxCtrl>() == false)
            {
                this.gameObject.GetComponent<FoxCtrl>().enabled = true;
            }

        }
        //포톤 추가
        //원격 플레이어일 때 수행
        else
        {
            //원격 플레이어의 아바타를 수신받은 위치까지 부드럽게 이동시키자
            myTr.position = Vector3.Lerp(myTr.position, currPos, Time.deltaTime * 3.0f);
            //원격 플레이어의 아바타를 수신받은 각도만큼 부드럽게 회전시키자
            myTr.rotation = Quaternion.Slerp(myTr.rotation, currRot, Time.deltaTime * 3.0f);
        }

    }

    IEnumerator ModeSet()
    {
        while (!isDie)
        {
            yield return new WaitForSeconds(0.2f);

            // traceTarget null 가드
            if (!myTr || !traceTarget)
            {
                state = sleep ? FoxState.수면 : FoxState.대기;
                continue;
            }

            float dist = Vector3.Distance(myTr.position, traceTarget.position);

            if (isStun)
                state = FoxState.스턴;
            else if (dist <= attackDist)
                state = FoxState.공격;
            else if (traceAttack || dist <= traceDist)
                state = FoxState.추적;
            else if (sleep)
                state = FoxState.수면;
            else if (roamingTimeSet > 0 && roamingPause == false)
                state = FoxState.정찰;
            else
                state = FoxState.대기;

        }
    }

    IEnumerator ModeAction()
    {

        while (!isDie)
        {
            switch (state)
            {
                case FoxState.대기:
                    if (myTraceAgent) myTraceAgent.isStopped = true;
                    if (anim) anim.SetBool("추적", false);
                    roamingTimeSet += Time.deltaTime * 5.0f;
                    if (roamingTimeSet > 9.9 && roamingPause == true)
                    { roamingPause = false; }

                    // 네트워크 동기화
                    net_Anim = 0;
                    pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);
                    break;

                case FoxState.추적:
                    if (myTraceAgent && traceTarget)
                    {
                        myTraceAgent.stoppingDistance = attackDist;
                        myTraceAgent.isStopped = false;
                        // 거리 2f 정도 떨어진 상태에서 공격 // 하게 하고 싶은데 잘 안됨... // 잘 되긴 함
                        myTraceAgent.destination = traceTarget.position;
                    }
                    if (anim) anim.SetBool("추적", true);
                    // 네트워크 동기화
                    net_Anim = 2;
                    pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);
                    break;

                case FoxState.수면:

                    // if (myTraceAgent) myTraceAgent.isStopped = true;
                    // if (anim) anim.SetBool("수면", true);
                    // // 네트워크 동기화
                    // net_Anim = 5;
                    // pv.RPC("RPC_SetAim", PhotonTargets.All, net_Anim);
                    break;
                case FoxState.공격:
                    break;
                case FoxState.스턴:
                    break;
                case FoxState.정찰:

                    Patrol();
                    break;

                    // 추후 추가 예정
            }

            yield return new WaitForSeconds((state == FoxState.정찰) ? 3f : 0.02f);
        }
    }
    IEnumerator TargetSetting()
    {
        while (!isDie)
        {
            yield return new WaitForSeconds(0.2f);

            players = GameObject.FindGameObjectsWithTag("Player");
            if (players == null || players.Length == 0 || !myTr) continue;

            // 가장 가까운 플레이어 선택
            Transform best = players[0].transform;
            float bestSqr = (best.position - myTr.position).sqrMagnitude;

            for (int i = 1; i < players.Length; i++)
            {
                var tr = players[i].transform;
                float sqr = (tr.position - myTr.position).sqrMagnitude;
                if (sqr < bestSqr) { best = tr; bestSqr = sqr; }
            }

            playerTarget = best;
            traceTarget = best;
        }
    }
    void Attack()
    {
        myTraceAgent.isStopped = true;
        // 빌보딩이 켜져있으면 공격을 못 피함 난이도 문제로 일단 주석
        Quaternion enemyLookRotation = Quaternion.LookRotation(traceTarget.position - myTr.position); // - 해줘야 바라봄  
        myTr.rotation = Quaternion.Lerp(myTr.rotation, enemyLookRotation, Time.deltaTime * 10.0f);

        anim.SetTrigger("공격");
        // 네트워크 동기화
        net_Anim = 3;
        pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);

    }

    public void Stun()
    {
        // 내 클라에서만 스턴 처리
        if (!pv.isMine) return; 
         
        // 스턴 상태가 아니면
        if (isStun == false)
        {
            //Debug.Log("스턴");
            myTraceAgent.isStopped = true;
            stunTime = 3f;
            isStun = true;
            state = FoxState.스턴;
            if (anim) anim.SetTrigger("스턴");
            if (anim) anim.SetBool("정지", true);
            

            // 네트워크 동기화
            net_Anim = 4;
            pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);
            net_Anim = 5;
            pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);
        }
        // 스턴 또 당하면 stunTime 3으로 초기화;
        else if (isStun == true)
        {
            myTraceAgent.isStopped = true;
            //Debug.Log("비상");
            if (anim) anim.SetBool("정지", true);
            // 네트워크 동기화
            net_Anim = 5;
            pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);


            stunTime = 3f; // 3으로 다시 초기화
        }
        else if (isStun == false && stunTime <= 0f)
        {
            state = FoxState.대기;
        }
    }

    void StunCool()
{
    // 마스터만 카운트다운
    if (!PhotonNetwork.isMasterClient) return;

    if (isStun)
    {
        stunTime -= Time.deltaTime;

        if (stunTime <= 0f)
        {
            isStun = false;
            stunTime = 0f;

            if (anim)
                anim.SetBool("정지", false);

            if (myTraceAgent && !isDie)
                myTraceAgent.isStopped = false;

            // 상태 복구: 일단 대기로 던지고 ModeSet에서 다시 판정
            state = FoxState.대기;

            net_Anim = 7; // 정지 해제
            pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);
        }
    }

    // 디버그: Y키로 강제 스턴
    if (Input.GetKeyDown(KeyCode.Y))
    {
        Stun();
    }
}
    // 조건1? 참1 : (조건2? 참2: 거짓2)
    void AutoRoaming(float radius)
    {
        if (roamingPause == false)
        {
            if (myTraceAgent.isStopped) myTraceAgent.isStopped = false;

            float headDir = UnityEngine.Random.Range(0f, 3f);
            StartCoroutine(RandomDir(headDir));

            Vector3 randomDir = UnityEngine.Random.insideUnitSphere * radius;
            // 0~6 랜덤게임 무슨 게임 게임 스타트 3보다 작으면 0, 아니야 3보다 커? 그럼 다시 돌려 3~6으로 4.5보다 작아? 작으면 3 아니야 더 커? 그럼 6 먹어 
            float randomY = UnityEngine.Random.Range(0f, 6f) < 3 ? 0 : UnityEngine.Random.Range(3f, 6f) < 4.5f ? 3f : 6f;
            //Debug.Log(randomY);
            randomDir.y = randomY;
            Vector3 target = transform.position + randomDir;

            if (NavMesh.SamplePosition(target, out var hit, radius, NavMesh.AllAreas))
                myTraceAgent.SetDestination(hit.position);
        }
    }

    void Patrol()
    {

        roamingTimeSet -= Time.deltaTime * 300f;
        if (anim)
        {
            anim.SetFloat("로밍시간", roamingTimeSet);

            // 네트워크 동기화
            net_Anim = 6;
            pv.RPC("RPC_SetAim", PhotonTargets.Others, net_Anim);
        }

        AutoRoaming(20f); // findDist를 써서 그냥 재활용 랜덤으로 포인트 잡아서 정찰하게 하는 함수


        if (roamingTimeSet <= 0.1)
        {
            roamingPause = true;
            state = FoxState.대기;
        }
    }

    IEnumerator RandomDir(float headDir)
    {
        Debug.Log("방향");
        anim.SetFloat("방향", headDir);
        yield return new WaitForSeconds(3f);

    }
    [PunRPC]
    public void RPC_Stun()
    {
        Stun();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        {
            // 마스터: 자신의 상태를 전송
            stream.SendNext(myTr.position);
            stream.SendNext(myTr.rotation);

            // 만약 애니메이션 상태가 바뀌었으면 그 값도 전송 
            // 그리고 네트워크 문제가 생겼으면 여기부터 의심해보자
            stream.SendNext(net_Anim);
        }
        else
        {
            // 클라이언트: 마스터가 보낸 값을 수신
            currPos = (Vector3)stream.ReceiveNext();
            currRot = (Quaternion)stream.ReceiveNext();
            net_Anim = (int)stream.ReceiveNext();
        }
    }

    [PunRPC]
    void RPC_SetAim(int net_Anim)
    {
        this.net_Anim = net_Anim;

        /* 준희가 임시로 짠 애니메이터 파라미터들
        if (this.net_Anim == 0)
        {
            //anim.SetBool("대기", false);
        }
        else if (this.net_Anim == 1)
        {
            anim.SetBool("정찰", true);
        }
        else if (this.net_Anim == 2)
        {
            anim.SetTrigger("추적");
        }
        else if (this.net_Anim == 3)
        {
            anim.SetTrigger("공격");
        }
        else if (this.net_Anim == 4)
        {
            anim.SetTrigger("스턴");
        }
        else if (this.net_Anim == 5)
        {
            anim.SetBool("정지", true);
        }
        else if (this.net_Anim == 6)
        {
            anim.SetFloat("로밍시간", roamingTimeSet);
        }
        else if (this.net_Anim == 7)
        {
            anim.SetBool("정지", false);
        }
        */

        if (this.net_Anim == 0)
        {
            //anim.SetBool("대기", false);        // 파라미터 부재
        }
        else if (this.net_Anim == 1)        //  현재 1번 미할당임
        {
            //anim.SetBool("정찰", true);         // 파라미터 부재
        }
        else if (this.net_Anim == 2)        // 네놈추
        {
            anim.SetBool("추적", true);
        }
        else if (this.net_Anim == 3)        // 얍얍
        {
            anim.SetTrigger("공격");
        }
        else if (this.net_Anim == 4)        // 읔엨
        {
            anim.SetTrigger("스턴");
        }
        else if (this.net_Anim == 5)        // 정지
        {
            anim.SetBool("정지", true);
        }
        else if (this.net_Anim == 6)        // 로밍
        {
            anim.SetFloat("로밍시간", roamingTimeSet);
        }
        else if (this.net_Anim == 7)        // 정지 해제
        {
            anim.SetBool("정지", false);
        }
    }
}
