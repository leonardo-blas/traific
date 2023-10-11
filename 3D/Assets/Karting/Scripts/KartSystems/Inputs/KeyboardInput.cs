using UnityEngine;
using Unity.Netcode;

namespace KartGame.KartSystems {

    public class KeyboardInput : BaseInput
    {
        public override Vector2 GenerateInput() {
            if (IsOwner)
            {
                return new Vector2 {
                    x = Input.GetAxis("Horizontal"),
                    y = Input.GetAxis("Vertical")
                };
            }

            return new Vector2(0, 0);
        }
    }
}
