using UnityEngine;

public class CreepyCrawler : MonoBehaviour
{
    [Header("【移動設定】")]
    public float moveSpeed = 0.8f;
    public float turnSpeed = 55f;

    [Header("【AI 隨機漫遊】")]
    public bool enableAIWander = true;
    public float changeDirectionInterval = 2f;
    private float aiTurnValue = 0f;
    private float aiTimer = 0f;

    [Header("【表面貼合】")]
    public LayerMask groundLayer;
    public float heightFromMesh = 0.25f;
    public float snapSpeed = 12f;
    public float normalAlignSpeed = 7f;
    public float castRadius = 0.12f;
    // 單幀 snap 最多能移動多遠（公尺）。防止目標點突然跳遠時「穿牆傳送」。
    // 太小：貼合變慢、上下坡跟不上；太大：又會穿牆。0.05 ≈ 在 72fps 下約 3.6 m/s 上限。
    public float maxSnapStep = 0.05f;

    [Header("【防掉落】")]
    public float edgeLookAhead = 0.5f;
    public float edgeTurnDuration = 0.7f;
    private float edgeTurnTimer = 0f;
    private float edgeTurnDir = 1f;

    [Header("【射線長度】")]
    public float downCastLength = 2.5f;
    public float forwardCastLength = 1.2f;

    [Header("【爬牆過渡】")]
    // 距離牆壁多近時開始「感應」到牆，準備過渡
    public float wallDetectDistance = 0.8f;
    // 過渡速度：越大越快翻上牆，太大會突然翻轉
    public float wallTransitionSpeed = 5f;

    [Header("【除錯顯示】")]
    // 打開後：Scene 視窗會畫出所有射線（綠=打到/紅=打空）、
    // 偵測到的表面點、法線與前進方向；Game 視窗左上角顯示狀態文字。
    public bool showDebug = true;

    private Vector3 currentNormal = Vector3.up;
    private bool onSurface = false;
    private Vector3 detectedSurfacePoint;

    void Start()
    {
        currentNormal = Vector3.up;
    }

    void Update()
    {
        DetectSurface();
        HandleTurning();
        MoveAlongSurface();
        SnapToSurface();
    }

    private void DetectSurface()
    {
        Vector3 origin = transform.position + currentNormal * heightFromMesh * 2f;

        Vector3 surfNormal, surfPoint;

        // ── 優先級 1：目前法線方向的正下方（維持當前爬行面）──
        if (CastAverage(origin, -currentNormal, downCastLength, out surfNormal, out surfPoint))
        {
            // 正常貼合，平滑更新法線
            currentNormal = Vector3.Slerp(currentNormal, surfNormal, normalAlignSpeed * Time.deltaTime).normalized;
            detectedSurfacePoint = surfPoint;
            onSurface = true;
        }
        else
        {
            onSurface = false;
        }

        // ── 優先級 2：前方牆壁感應（不管有沒有踩到地都要跑）──
        // 從頭部前方打一條射線，看看多遠有牆
        Vector3 forwardOrigin = transform.position + currentNormal * heightFromMesh;
        if (CastAverage(forwardOrigin, transform.forward, wallDetectDistance, out surfNormal, out surfPoint))
        {
            // 牆的法線跟目前法線差很多（表示真的是不同面），才進行過渡
            float angleDiff = Vector3.Angle(currentNormal, surfNormal);
            if (angleDiff > 30f)
            {
                // 距離越近，過渡越快（快碰到牆了就趕快翻）
                float distToWall = Vector3.Distance(transform.position, surfPoint);
                float blendFactor = 1f - Mathf.Clamp01(distToWall / wallDetectDistance);
                float transitionRate = wallTransitionSpeed * blendFactor;

                currentNormal = Vector3.Slerp(currentNormal, surfNormal, transitionRate * Time.deltaTime).normalized;
                // 注意：這裡【刻意不更新 detectedSurfacePoint】。
                // snap 的目標永遠只由「腳下的下射線」決定（優先級 1 / 3），
                // 爬牆改靠 currentNormal 慢慢轉向牆面、讓下射線自然轉去打到牆，
                // 這樣就不會把 snap 目標瞬間拉到 0.8m 外造成穿牆爆衝。
                onSurface = true;
            }
        }

        // ── 優先級 3：往斜前下方打（補上地板→牆的角落盲區）──
        // 方向 = forward 和 -currentNormal 的中間，用來偵測「牆角轉角」
        Vector3 cornerDir = (transform.forward - currentNormal).normalized;
        if (!onSurface && CastAverage(origin, cornerDir, downCastLength, out surfNormal, out surfPoint))
        {
            currentNormal = Vector3.Slerp(currentNormal, surfNormal, normalAlignSpeed * Time.deltaTime).normalized;
            detectedSurfacePoint = surfPoint;
            onSurface = true;
        }
    }

    private void HandleTurning()
    {
        float turnInput = 0f;
        float playerInput = Input.GetAxis("Horizontal");

        if (Mathf.Abs(playerInput) > 0.1f)
        {
            turnInput = playerInput;
            edgeTurnTimer = 0f;
        }
        else if (edgeTurnTimer > 0f)
        {
            edgeTurnTimer -= Time.deltaTime;
            turnInput = edgeTurnDir;
        }
        else if (enableAIWander)
        {
            aiTimer -= Time.deltaTime;
            if (aiTimer <= 0f)
            {
                aiTurnValue = Random.Range(-1f, 1f);
                aiTimer = Random.Range(0.5f, changeDirectionInterval);
            }
            turnInput = aiTurnValue;
        }

        // 繞表面法線旋轉（在任何面上轉向都正確）
        transform.rotation = Quaternion.AngleAxis(turnInput * turnSpeed * Time.deltaTime, currentNormal) * transform.rotation;
    }

    private void MoveAlongSurface()
    {
        if (!onSurface) return;

        // 前進方向投影到表面切線平面，永遠貼著走
        Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, currentNormal).normalized;

        // 邊緣探照燈
        Vector3 lookAheadOrigin = transform.position + moveDir * edgeLookAhead + currentNormal * heightFromMesh * 2f;
        bool edgeAhead = !Physics.Raycast(lookAheadOrigin, -currentNormal, downCastLength + 0.5f, groundLayer);

        if (edgeAhead && edgeTurnTimer <= 0f)
        {
            // 邊緣前先檢查有沒有牆可以爬，有牆就不觸發轉向
            Vector3 forwardOrigin = transform.position + currentNormal * heightFromMesh;
            bool wallAhead = Physics.Raycast(forwardOrigin, transform.forward, wallDetectDistance, groundLayer);
            
            if (!wallAhead)
            {
                edgeTurnDir   = (Random.value > 0.5f) ? 1f : -1f;
                edgeTurnTimer = edgeTurnDuration;
            }
        }

        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    private void SnapToSurface()
    {
        if (!onSurface) return;

        Vector3 targetPos = detectedSurfacePoint + currentNormal * heightFromMesh;
        // 先算出 Lerp 想去的位置，再用 MoveTowards 限制這一幀「最多只能移動 maxSnapStep」。
        // 雙保險：平常 Lerp 平順貼合；目標突然跳遠時被 maxSnapStep 卡住，永遠不會穿牆傳送。
        Vector3 lerped = Vector3.Lerp(transform.position, targetPos, snapSpeed * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, lerped, maxSnapStep);

        Quaternion targetRot = Quaternion.FromToRotation(transform.up, currentNormal) * transform.rotation;
        transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot, normalAlignSpeed * Time.deltaTime);
    }

    private bool CastAverage(Vector3 origin, Vector3 dir, float length,
                              out Vector3 avgNormal, out Vector3 avgPoint)
    {
        avgNormal = Vector3.zero;
        avgPoint  = Vector3.zero;
        int hits  = 0;

        Vector3 axisA = Vector3.Cross(dir, transform.right).normalized;
        Vector3 axisB = transform.right;

        Vector3[] offsets = {
            Vector3.zero,
            axisA *  castRadius,
            axisA * -castRadius,
            axisB *  castRadius,
            axisB * -castRadius,
        };

        foreach (var offset in offsets)
        {
            if (Physics.Raycast(origin + offset, dir, out RaycastHit hit, length, groundLayer))
            {
                avgNormal += hit.normal;
                avgPoint  += hit.point;
                hits++;
                // 綠線：這條射線打到了，畫到命中點
                if (showDebug) Debug.DrawLine(origin + offset, hit.point, Color.green);
            }
            else if (showDebug)
            {
                // 紅線：這條射線打空了（畫滿整條長度）
                Debug.DrawRay(origin + offset, dir * length, Color.red);
            }
        }

        if (hits > 0)
        {
            avgNormal = (avgNormal / hits).normalized;
            avgPoint  =  avgPoint  / hits;
            return true;
        }
        return false;
    }

    // ── 在 Scene 視窗畫出關鍵向量（只在 Play 中且 showDebug 開啟時）──
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;

        // 黃球＝目前 snap 要追的表面點（onSurface 為 false 時變灰）
        Gizmos.color = onSurface ? Color.yellow : Color.gray;
        Gizmos.DrawSphere(detectedSurfacePoint, 0.03f);

        // 青線＝目前的表面法線（身體會沿這個方向抬高 / 貼平）
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + currentNormal * 0.3f);

        // 洋紅線＝實際前進方向（投影到表面後）
        Gizmos.color = Color.magenta;
        Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, currentNormal).normalized;
        Gizmos.DrawLine(transform.position, transform.position + moveDir * 0.3f);
    }

    // ── 在 Game 視窗左上角顯示即時狀態，方便看「停住」時 onSurface 是不是 false ──
    private void OnGUI()
    {
        if (!showDebug) return;
        GUI.color = onSurface ? Color.green : Color.red;
        GUI.Label(new Rect(12, 12, 400, 24),
            $"onSurface = {onSurface}    edgeTurnTimer = {edgeTurnTimer:F2}");
    }
}