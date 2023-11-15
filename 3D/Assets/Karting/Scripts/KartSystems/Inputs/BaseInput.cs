using Unity.Netcode;
using UnityEngine;

namespace KartGame.KartSystems
{

    public interface IInput
    {
        Vector2 GenerateInput();
    }

    public abstract class BaseInput : NetworkBehaviour, IInput
    {
        /// <summary>
        /// Override this function to generate an XY input that can be used to steer and control the car.
        /// </summary>
        public abstract Vector2 GenerateInput();
    }
}
