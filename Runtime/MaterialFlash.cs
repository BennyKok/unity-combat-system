// using DG.Tweening;
// using UnityEngine;

// namespace BennyKok.CombatSystem
// {
//     [RequireComponent(typeof(CombatEntity))]
//     public class MaterialFlash : MonoBehaviour
//     {
//         public Renderer targetRenderer;
//         public Renderer[] targetRenderers;
//         public string damageFlashMatFieldName = "Flash";
//         public bool invert;

//         private void Start()
//         {
//             gameObject.GetComponent<CombatEntity>().onDamage += (pt, from) =>
//             {
//                 DOTween.To(() => invert ? 1f : 0f, x =>
//                 {
//                     if (targetRenderer && targetRenderer.material)
//                         targetRenderer.material.SetFloat(damageFlashMatFieldName, x);

//                     if (targetRenderers != null && targetRenderers.Length > 0)
//                     {
//                         foreach (var renderer in targetRenderers)
//                         {
//                             if (renderer && renderer.material)
//                                 renderer.material.SetFloat(damageFlashMatFieldName, x);
//                         }
//                     }
//                 }, invert ? 0f : 1f, 0.1f).SetEase(Ease.Linear).SetLoops(2, LoopType.Yoyo).SetAutoKill().SetId(GetInstanceID()).Play();
//             };
//         }
//     }
// }