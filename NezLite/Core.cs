
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nez.BitmapFonts;
using Nez.Systems;
using Nez.Textures;
using Nez.Timers;
using Nez.Tweens;
using System;
using System.Collections;
using Microsoft.Xna.Framework.Content;

namespace Nez
{
    public class Core : Game
    {   /// <summary>
        /// core emitter. emits only Core level events.
        /// </summary>
        public static Emitter<CoreEvents> Emitter { get; private set; }
        public static SamplerState DefaultSamplerState { get; set; } = SamplerState.PointClamp;
        public new static GraphicsDevice GraphicsDevice { get; private set; }
        public new static NezContentManager Content;

        internal static Core _instance;

        private string _windowTitle;
        /// <summary>
        /// used to coalesce GraphicsDeviceReset events
        /// </summary>
        ITimer _graphicsDeviceChangeTimer;

        // globally accessible systems
        FastList<GlobalManager> _globalManagers = new FastList<GlobalManager>();
        CoroutineManager _coroutineManager = new CoroutineManager();
        TimerManager _timerManager = new TimerManager();

        public Core(int width = 1280, int height = 720, bool isFullScreen = false, string windowTitle = "Nez", string contentDirectory = "Content")
        {
#if DEBUG
            _windowTitle = windowTitle;
#endif
            _instance = this;
 
            Emitter = new Emitter<CoreEvents>(new CoreEventsComparer());

            var graphicsManager = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = width,
                PreferredBackBufferHeight = height,
                IsFullScreen = isFullScreen,
                SynchronizeWithVerticalRetrace = true,
//#if MONOGAME_38
				PreferHalfPixelOffset = true
//#endif
            };
            graphicsManager.DeviceReset += OnGraphicsDeviceReset;
            graphicsManager.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;

            Screen.Initialize(graphicsManager);
            Window.ClientSizeChanged += OnGraphicsDeviceReset;
            Window.OrientationChanged += OnOrientationChanged;

            base.Content.RootDirectory = contentDirectory;
            Content = new NezGlobalContentManager(Services, base.Content.RootDirectory);

            IsMouseVisible = true;
            IsFixedTimeStep = false;

            // setup systems
            RegisterGlobalManager(_coroutineManager);
            RegisterGlobalManager(new TweenManager());
            RegisterGlobalManager(_timerManager);
            RegisterGlobalManager(new RenderTarget());
        }

        /// <summary>
        /// this gets called whenever the screen size changes
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        protected void OnGraphicsDeviceReset(object sender, EventArgs e)
        {
            // we coalese these to avoid spamming events
            if (_graphicsDeviceChangeTimer != null)
            {
                _graphicsDeviceChangeTimer.Reset();
            }
            else
            {
                _graphicsDeviceChangeTimer = Schedule(0.05f, false, this, t =>
                {
                    (t.Context as Core)._graphicsDeviceChangeTimer = null;
                    Emitter.Emit(CoreEvents.GraphicsDeviceReset);
                });
            }
        }

        void OnOrientationChanged(object sender, EventArgs e)
        {
            Emitter.Emit(CoreEvents.OrientationChanged);
        }

        protected override void Initialize()
        {
            base.Initialize();

            // prep the default Graphics system
            GraphicsDevice = base.GraphicsDevice;
            var font = Content.Load<BitmapFont>("nez://Nez.Content.NezDefaultBMFont.xnb");
            Graphics.Instance = new Graphics(font);
        }

        /// <summary>
        /// Don't override this, override the one without parameters
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime)
        {
            //if (PauseOnFocusLost && !IsActive)
            //{
            //    SuppressDraw();
            //    return;
            //}

            // update all our systems and global managers
            Time.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
            Input.Update();

            //if (ExitOnEscapeKeypress &&
            //    (Input.IsKeyDown(Keys.Escape) || Input.GamePads[0].IsButtonReleased(Buttons.Back)))
            //{
            //    base.Exit();
            //    return;
            //}

            for (var i = _globalManagers.Length - 1; i >= 0; i--)
            {
                if (_globalManagers.Buffer[i].Enabled)
                    _globalManagers.Buffer[i].Update();
            }

            Update();
        }

        protected override void Draw(GameTime gameTime)
        {
            Draw();
        }

        /// <summary>
        /// Override this for your update loop and just use Time class to get time info, no need to call base
        /// </summary>
        protected virtual void Update()
        {

        }

        /// <summary>
        /// Override this for your draw loop and just use Time class to get time info, no need to call base
        /// </summary>
        protected virtual void Draw()
        {

        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            base.OnExiting(sender, args);
            Emitter.Emit(CoreEvents.Exiting);
        }

        #region Global Managers

        /// <summary>
        /// adds a global manager object that will have its update method called each frame before Scene.update is called
        /// </summary>
        /// <returns>The global manager.</returns>
        /// <param name="manager">Manager.</param>
        public static void RegisterGlobalManager(GlobalManager manager)
        {
            _instance._globalManagers.Add(manager);
            manager.Enabled = true;
        }

        /// <summary>
        /// removes the global manager object
        /// </summary>
        /// <returns>The global manager.</returns>
        /// <param name="manager">Manager.</param>
        public static void UnregisterGlobalManager(GlobalManager manager)
        {
            _instance._globalManagers.Remove(manager);
            manager.Enabled = false;
        }

        /// <summary>
        /// gets the global manager of type T
        /// </summary>
        /// <returns>The global manager.</returns>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public static T GetGlobalManager<T>() where T : GlobalManager
        {
            for (var i = 0; i < _instance._globalManagers.Length; i++)
            {
                if (_instance._globalManagers.Buffer[i] is T)
                    return _instance._globalManagers.Buffer[i] as T;
            }

            return null;
        }

        #endregion

        #region Systems access

        /// <summary>
        /// starts a coroutine. Coroutines can yeild ints/floats to delay for seconds or yeild to other calls to startCoroutine.
        /// Yielding null will make the coroutine get ticked the next frame.
        /// </summary>
        /// <returns>The coroutine.</returns>
        /// <param name="enumerator">Enumerator.</param>
        public static ICoroutine StartCoroutine(IEnumerator enumerator)
        {
            return _instance._coroutineManager.StartCoroutine(enumerator);
        }

        /// <summary>
        /// schedules a one-time or repeating timer that will call the passed in Action
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds.</param>
        /// <param name="repeats">If set to <c>true</c> repeats.</param>
        /// <param name="context">Context.</param>
        /// <param name="onTime">On time.</param>
        public static ITimer Schedule(float timeInSeconds, bool repeats, object context, Action<ITimer> onTime)
        {
            return _instance._timerManager.Schedule(timeInSeconds, repeats, context, onTime);
        }

        /// <summary>
        /// schedules a one-time timer that will call the passed in Action after timeInSeconds
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds.</param>
        /// <param name="context">Context.</param>
        /// <param name="onTime">On time.</param>
        public static ITimer Schedule(float timeInSeconds, object context, Action<ITimer> onTime)
        {
            return _instance._timerManager.Schedule(timeInSeconds, false, context, onTime);
        }

        /// <summary>
        /// schedules a one-time or repeating timer that will call the passed in Action
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds.</param>
        /// <param name="repeats">If set to <c>true</c> repeats.</param>
        /// <param name="onTime">On time.</param>
        public static ITimer Schedule(float timeInSeconds, bool repeats, Action<ITimer> onTime)
        {
            return _instance._timerManager.Schedule(timeInSeconds, repeats, null, onTime);
        }

        /// <summary>
        /// schedules a one-time timer that will call the passed in Action after timeInSeconds
        /// </summary>
        /// <param name="timeInSeconds">Time in seconds.</param>
        /// <param name="onTime">On time.</param>
        public static ITimer Schedule(float timeInSeconds, Action<ITimer> onTime)
        {
            return _instance._timerManager.Schedule(timeInSeconds, false, null, onTime);
        }

        #endregion
    }
}
