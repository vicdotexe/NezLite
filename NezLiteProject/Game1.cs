using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nez;

namespace NezLiteProject
{
    public class Game1 : Core
    {


        public Game1() : base()
        {

        }


        protected override void LoadContent()
        {

        }

        protected override void Update()
        {

        }

        protected override void Draw()
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            Graphics.Instance.Batcher.Begin();
            Graphics.Instance.Batcher.DrawCircle(Input.MousePosition, 10, Color.Yellow);
            Graphics.Instance.Batcher.DrawString(Graphics.Instance.BitmapFont, "Test String", Input.MousePosition + new Vector2(50,50), Color.Red);
            Graphics.Instance.Batcher.End();
        }
    }
}