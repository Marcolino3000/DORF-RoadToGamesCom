using System.Collections;
using UnityEngine;

namespace ScenesSwitches
{
    public class TrainMover : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vector2 endPositionYZ;
        [SerializeField] private float duration = 5f;
        [SerializeField] private float maxSpeed = 10f;
        [SerializeField] private float stopDistance = 0f;
        [SerializeField] private AnimationCurve speedCurve;

        private Coroutine moveRoutine;

        [ContextMenu("Start Moving")]
        public void MoveTowardsCamera()
        {
            if (moveRoutine != null)
                StopCoroutine(moveRoutine);

            moveRoutine = StartCoroutine(MoveTowardsCameraRoutine());
        }

        private IEnumerator MoveTowardsCameraRoutine()
        {
            Vector3 targetPosition = new Vector3(transform.position.x, endPositionYZ.x, endPositionYZ.y);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float normalizedTime = elapsed / duration;
                float speed = speedCurve.Evaluate(normalizedTime) * maxSpeed;

                Vector3 toTarget = targetPosition - transform.position;
                float distance = toTarget.magnitude;

                if (distance <= stopDistance)
                    break;

                Vector3 direction = toTarget / distance;
                float step = Mathf.Min(speed * Time.deltaTime, distance - stopDistance);
                transform.position += direction * step;

                elapsed += Time.deltaTime;
                yield return null;
            }

            moveRoutine = null;
        }
    }
}
