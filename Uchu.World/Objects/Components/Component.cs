namespace Uchu.World
{
    public abstract class Component : Object
    {
        protected Component()
        {
            Listen(OnDestroyed, async () =>
            {
                await GameObject.RemoveComponentAsync(this, false);
            });
        }

        public GameObject GameObject { get; set; }

        public Transform Transform => GameObject.Transform;
    }
}