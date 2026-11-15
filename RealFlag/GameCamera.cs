using UnityEngine;

namespace RealFlag
{
    /// <summary>
    /// The game's own camera: the one its CameraController moves and the city is drawn with. The
    /// flags follow its view: what is in sight and how big on the screen (FlagRenderer).
    /// </summary>
    /// <remarks>
    /// Not Camera.main, which is the first ENABLED camera tagged MainCamera: a mod that renders the
    /// game's camera by hand takes that away. Dynamic Resolution (812713438) and Lumina's Dynamic
    /// Resolution / SSAA (2983036781) disable the game's camera, make an empty camera of their own
    /// (culling mask 0, depth -3) tagged MainCamera that copies its pose, and in that camera's
    /// OnRenderImage call the game camera's Render() into a target the screen's size times a factor
    /// (IL of both, 2026-10-14 and 2026-11-15). Camera.main is then the empty one, and a mesh
    /// queued for it is drawn nowhere: every flag was gone with the mod merely enabled. A camera
    /// rendered by hand still runs Camera.onPreRender, and draws a mesh queued for it only in its
    /// first render of a frame (the preview's Dynamic Resolution probe, 2026-11-15): the flags'
    /// meshes are queued for every camera, as props are (FlagRenderer.EveryCamera). Where no
    /// CameraController exists, Camera.main as before. Without such a mod the two are the same
    /// camera: this changes nothing, and logs nothing. The same class as Real Glass's and
    /// Volumetric Weather's.
    /// </remarks>
    public static class GameCamera
    {
        private static CameraController _controller;
        private static Camera _camera;
        private static float _nextSearch;

        private static Camera _loggedGame, _loggedMain;
        private static bool _logged;

        /// <summary>The game's camera controller, or null where there is none (looked up at most once a second).</summary>
        public static CameraController Controller
        {
            get
            {
                // FindObjectOfType walks every object: not every frame while there is none. A
                // controller destroyed with its scene compares equal to null and is looked up again.
                if (_controller == null && Time.realtimeSinceStartup >= _nextSearch)
                {
                    _nextSearch = Time.realtimeSinceStartup + 1f;
                    _controller = Object.FindObjectOfType<CameraController>();
                    _camera = null;
                }

                return _controller;
            }
        }

        /// <summary>The game's camera, else Camera.main; null when there is neither.</summary>
        public static Camera Get()
        {
            if (_camera == null)
            {
                // CameraController.Awake takes the Camera on its own GameObject (IL): the game's camera.
                CameraController controller = Controller;
                if (controller != null)
                    _camera = controller.GetComponent<Camera>();
            }

            Camera main = Camera.main;
            Camera game = _camera != null ? _camera : main;
            if (!_logged || game != _loggedGame || main != _loggedMain)
            {
                // One line a change, and only when it says something: the two differ, or did.
                if (game != main || (_logged && _loggedGame != _loggedMain))
                {
                    Log.Msg(game == main
                        ? "camera: Camera.main is the game's camera '" + Name(game) + "' again"
                        : "camera: the city is drawn by the game's camera '" + Name(game) + "'" +
                          (game != null && !game.enabled ? " (disabled: another mod renders it itself)" : "") +
                          ", while Camera.main is '" + Name(main) + "': the flags follow the game's camera");
                }

                _logged = true;
                _loggedGame = game;
                _loggedMain = main;
            }

            return game;
        }

        /// <summary>A city unloaded: look the camera up again in the next (the old one is gone with its scene).</summary>
        public static void Forget()
        {
            _controller = null;
            _camera = null;
            _nextSearch = 0f;
            _logged = false;
            _loggedGame = null;
            _loggedMain = null;
        }

        private static string Name(Camera camera)
        {
            return camera != null ? camera.name : "none";
        }
    }
}
