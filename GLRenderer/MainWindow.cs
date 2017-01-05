using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenTK;
using OpenTK.Input;
using EnableCap = OpenTK.Graphics.OpenGL.EnableCap;
using GL = OpenTK.Graphics.OpenGL.GL;
using OpenTK.Graphics.OpenGL;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using Ejemplo2;
using Transformation;
using System.Linq;
using System.Runtime.InteropServices;

namespace PixelShader
{
    
    //Struct de vertex, para usar luego:
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    { 
        public Vector3 position;
        public Vector3 normal;
        public Vector2 tex_coord;
        public Vector3 color;
    }


    //Representa un objeto. Para poder hacer display de mas de un mesh por escena:
    class Object
    {
        public Vector3[] VertexPositionData { get; }
        public Vector3[] VertexNormalData { get; }
        public Vector3[] VertexTextureData { get; }
        public uint[] FacesData { get; }
        public float[] MaterialColor = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
        public int shininess { get; set; }
        public Vector3 center { get; set; }

        public float[] RotationMatrix { get; set; }
        public float[] TranslationMatrix { get; set; }
        public float[] T1Matrix { get; set; }
        public float[] T2Matrix { get; set; }
        public float[] ScaleMatrix { get; set; }
        public float[] TransformationMatrix { get; set; }

        public int rotationMatrixHandle;
        public int translationMatrixHandle;
        public int t1MatrixHandle;
        public int t2MatrixHandle;
        public int scaleMatrixHandle;
        public int VertexPositionBufferHandle { get; set; }
        public int VertexNormalBufferHandle { get; set; }
        public int VertexTextureBufferHandle { get; set; }
        public int FacesBufferHandle { get; set; }
        public int ObjectHandle { get; set; }
        public int TextureId { get; set; }

        public Object(Vector3[] vertexPositionData, Vector3[] vertexNormalData, Vector3[] vertexTextureData, uint[] facesData, float[] transformationMatrix, float[] materialColor)
        {
            VertexPositionData = vertexPositionData;
            VertexNormalData = vertexNormalData;
            VertexTextureData = vertexTextureData;
            FacesData = facesData;
            MaterialColor = materialColor;

            TransformationMatrix = transformationMatrix;
            RotationMatrix = new float[]    { 1, 0, 0, 0,    0, 1, 0, 0,    0, 0, 1, 0,    0, 0, 0, 1 };
            TranslationMatrix = new float[] { 1, 0, 0, 0,    0, 1, 0, 0,    0, 0, 1, 0,    0, 0, 0, 1 };
            ScaleMatrix = new float[]       { 1, 0, 0, 0,    0, 1, 0, 0,    0, 0, 1, 0,    0, 0, 0, 1 };
        }
    }


    //Clase que hereda de gamewindow, opengl.
    class MainWindow : GameWindow
    {
        //Parametros de escena y auxiliares:
        public static Scene scene { get; set; }
        public static int raster_mode { get; set; }
        public static int SceneWidth = 1280;
        public static int SceneHeight = 720;
        public static float FrameRate = 60;
        private static long currentTime { get; set; }
        private static float deltaTime { get; set; }
        private static float mousePrevX;
        private static float mousePrevY;
        private static int camera_mode = 0;
        private static tuple_3 floor_normal = new tuple_3(0,0,1);

        //non-static variables:
        private bool mouseClicked { get; set; }
        private float jump_velocity = 7f;
        private float jump_time = 0.0f;
        private float min_z = SceneHeight / 2f;
        private float g = 18f;
        private float rotation_angle = 1;

        //Para hacer la animacion de cambio de camara:
        private bool camera_switching { get; set; }
        private tuple_3 delta_tgt { get; set; }
        private tuple_3 delta_pos { get; set; }
        private int camera_frames;

        //Shaders:
        private string VertexShaderFilePath = Path.Combine("Shaders", "VertexShader.glsl");
        private string PixelShaderFilePath = Path.Combine("Shaders", "PixelShader.glsl");
        private string _vertexShaderSource;
        private string _pixelShaderSource;
        private int _pixelShaderHandle;
        private int _vertexShaderHandle;
        private int _shaderProgramHandle;

        //Handles para los UNIFORMS:
        private int _transformationMatrixHandle;
        private int _lightPositionArrayHandle;
        private int _lightColorArrayHandle;
        private int _lightCountHandle;
        private int _cameraPositionHandle;
        private int _materialColorHandle;
        private int _shininess_handle;
        private int _ambient_handle;
        private int _textureHandle;

        //Scene parameters:
        private float[] _cameraPosition;
        private float[] _lightPositionArray;
        private float[] _lightColorArray;
        private float[] _ambientLight;
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<Object> _static_obj = new List<Object>();
        private readonly List<Object> _animated_obj = new List<Object>();
        private Player player;


        //Constructor de MainWindow:
        public MainWindow(int width, int height)
          : base(width, height, new OpenTK.Graphics.GraphicsMode(), "GL Renderer", GameWindowFlags.Default, DisplayDevice.Default, 3, 0,
            OpenTK.Graphics.GraphicsContextFlags.ForwardCompatible | OpenTK.Graphics.GraphicsContextFlags.Debug)
        {
            //*CARGA ESCENA EN SCENE. AQUI PONER PATH DEL JSON QUE SE QUIERA LEER *********
            string sceneFilePath = Path.Combine("Json", "scene5.json");
            scene = Scene.LoadScene(sceneFilePath, SceneWidth, SceneHeight);

            //Posicion en que aparece la ventana:
            this.X = 100;
            this.Y = 20;

            //Mouse y keyboard
            Mouse.Move += mouseMoved;
            Mouse.WheelChanged += wheelchanged;
            KeyPress += KeyPressed;
            Keyboard.KeyDown += KeyDown;
            Keyboard.KeyUp += KeyUp;
            Mouse.ButtonDown += clicked;
            Mouse.ButtonUp += unclicked;
            

            float[] _transformationMatrix = get_transformation_matrix(width, height);
            _cameraPosition = new float[] { scene.Camera.position.x, scene.Camera.position.y, scene.Camera.position.z };

            //Aqui va a dejar seteado todos los objetos de la escena como objetos de OpenGL:
            foreach (var obj in scene.Objects)
            {
                //CARGA TODA LA INFORMACION DE FACES Y VERTEX DESDE OBJ: 
                uint[] _facesData = new uint[obj.faces.Count * 3];
                Vector3[] _vertexPositionData = new Vector3[obj.vertex.Count];
                Vector3[] _vertexNormalData = new Vector3[obj.vertex.Count];
                Vector3[] _vertexTextureData = new Vector3[obj.vt_dic.Count]; //Lo tenia como misma cant de vertices pero lo cambie porque tiraba index error.

                int face_index = 0; int vertex_index = 0;
                foreach (tuple_3[] face in obj.faces)
                {
                    _facesData[face_index++] = (uint)face[0].x - 1; //Estandar OPENGL (vertices parten en 0, no 1)
                    _facesData[face_index++] = (uint)face[0].y - 1; //Estandar OPENGL (vertices parten en 0, no 1)
                    _facesData[face_index++] = (uint)face[0].z - 1; //Estandar OPENGL (vertices parten en 0, no 1)
                }

                //Vertex position data:
                foreach (tuple_3 vertex in obj.vertex.Values)
                    _vertexPositionData[vertex_index++] = new Vector3(vertex.x, vertex.y, vertex.z);

                //Vertex Normal and Texture data:
                _vertexNormalData = obj.vn_array;
                if (obj.vt)
                    _vertexTextureData = obj.vt_array;

                //Pasa los datos de materiales a el array que corresponda. POR AHORA ASUMO 1 LAMBERT Y UN BLINNPHONG MAXIMO.
                float[] mat_color = new float[6];
                int textureId = set_material_handle(obj, ref mat_color);

                //Se crea el nuevo objeto:
                if (obj.player)
                {
                    player = new Player(_vertexPositionData, _vertexNormalData, _vertexTextureData, _facesData, _transformationMatrix, mat_color);
                    player.RotationMatrix = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }; 
                    player.T1Matrix = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                    player.T2Matrix = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                    set_material_shininess(obj, player);
                    player.TextureId = textureId;
                    player.center = new Vector3(obj.center.x, obj.center.y, obj.center.z);
                    _objects.Add(player);
                }
                else
                {
                    var new_obj = new Object(_vertexPositionData, _vertexNormalData, _vertexTextureData, _facesData, _transformationMatrix, mat_color);
                    new_obj.RotationMatrix = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                    new_obj.T1Matrix = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                    new_obj.T2Matrix = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
                    set_material_shininess(obj, new_obj);
                    new_obj.TextureId = textureId;
                    new_obj.center = new Vector3(obj.center.x, obj.center.y, obj.center.z);

                    //Se mete objeto a la lista que corresponda:
                    if (obj.animate)   _animated_obj.Add(new_obj);
                    else   _static_obj.Add(new_obj);
                    _objects.Add(new_obj);
                }
            }

            //Experimento a ver si puedo guardar las posiciones de las luces en un buffer y pasarlo como uniform:
            int light_index = 0; int max_lights = 10;
            _lightPositionArray = new float[max_lights * 3];
            _lightColorArray = new float[max_lights * 3];
            _ambientLight = new float[3];

            for (int i = 0; i < max_lights + 1; i++)
            {
                if (i < scene.Lights.Count)
                {
                    var l = scene.Lights[i];
                    if (l.name == "ambient_light")
                    {
                        AmbientLight a_light = (AmbientLight)l;
                        _ambientLight[0] = a_light.color[0];
                        _ambientLight[1] = a_light.color[1];
                        _ambientLight[2] = a_light.color[2];
                        continue;
                    }

                    PointLight light = (PointLight)l;

                    _lightColorArray[light_index] = light.color[0];
                    _lightPositionArray[light_index++] = light.position.x;
                    _lightColorArray[light_index] = light.color[1];
                    _lightPositionArray[light_index++] = light.position.y;
                    _lightColorArray[light_index] = light.color[2];
                    _lightPositionArray[light_index++] = light.position.z;
                }
                else
                {
                    _lightColorArray[light_index] = 0.0f;
                    _lightPositionArray[light_index++] = 0.0f;
                }
            }

            //Setea posicion inicial de player para que este con la camara:
            Vector3 pcenter = player.center;
            Vector3 dif = new Vector3(scene.Camera.position.x, scene.Camera.position.y, scene.Camera.position.z) - pcenter;
            matrix_4 t_player = new matrix_4(1,0,0,0,  0,1,0,0,  0,0,1,0, dif.X,dif.Y,0,1);
            player.TranslationMatrix = matrix4_to_array(t_player);
            tuple_3 w = Vectores.Normalize(scene.Camera.position - scene.Camera.target);
            tuple_3 u = Vectores.Normalize(Vectores.cross(scene.Camera.up, w));
            tuple_3 f = Vectores.Normalize(Vectores.cross(floor_normal, u));
            player.face_vector = new tuple_3(player.center.X, player.center.Y, player.center.Z) + f;
        }


        //The initialization method in an OpenGL program is called once.
        protected override void OnLoad(EventArgs e)
        {
            //Setea el tiempo para tener el delta en cada frame:
            currentTime = DateTime.Now.Ticks;

            GL.DepthRange(1, 0);
            List<float> bgc = ((List<object>)scene.Parameters["background_color"]).Select(Convert.ToSingle).ToList();
            VSync = VSyncMode.On;
            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(bgc[0], bgc[1], bgc[2], 1);

            LoadShaders();
            CreateShaders();
            CreateBuffers();
            CreateVertexArrays();

            Console.WriteLine(GL.GetString(StringName.Version));
        }
        

        //Deja cargados los shader sources:
        private void LoadShaders()
        {
            _pixelShaderSource = File.ReadAllText(PixelShaderFilePath);
            _vertexShaderSource = File.ReadAllText(VertexShaderFilePath);
        }


        // Shader source code is stored in GPU but we need to pass it to the GPU and instruct it to compile the code and create the program:
        protected virtual void CreateShaders()
        {
            //Crea los handles, numeros que serviran para representar shaders en GPU:
            _vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
            _pixelShaderHandle = GL.CreateShader(ShaderType.FragmentShader);

            //Une los handles al codigo fuente de los shaders:
            GL.ShaderSource(_vertexShaderHandle, _vertexShaderSource);
            GL.ShaderSource(_pixelShaderHandle, _pixelShaderSource);

            //Los compila:
            GL.CompileShader(_vertexShaderHandle);
            GL.CompileShader(_pixelShaderHandle);
            Console.WriteLine(GL.GetShaderInfoLog(_vertexShaderHandle));
            Console.WriteLine(GL.GetShaderInfoLog(_pixelShaderHandle));

            //Crea un handle para el programa, y luego le anexa los handles de los shaders:
            _shaderProgramHandle = GL.CreateProgram();
            GL.AttachShader(_shaderProgramHandle, _vertexShaderHandle);
            GL.AttachShader(_shaderProgramHandle, _pixelShaderHandle);
            GL.LinkProgram(_shaderProgramHandle);

            //Handles para luego poder pasar los uniforms al shader:
            _transformationMatrixHandle = GL.GetUniformLocation(_shaderProgramHandle, "transformationMatrix");
            _cameraPositionHandle = GL.GetUniformLocation(_shaderProgramHandle, "cameraPosition");
            _textureHandle = GL.GetUniformLocation(_shaderProgramHandle, "textureSampler");
            _lightPositionArrayHandle = GL.GetUniformLocation(_shaderProgramHandle, "lightPositionArray");
            _lightColorArrayHandle = GL.GetUniformLocation(_shaderProgramHandle, "lightColorArray");
            _materialColorHandle = GL.GetUniformLocation(_shaderProgramHandle, "materialColorArray");
            _lightCountHandle = GL.GetUniformLocation(_shaderProgramHandle, "lightCount");
            _shininess_handle = GL.GetUniformLocation(_shaderProgramHandle, "shininess");
            _ambient_handle = GL.GetUniformLocation(_shaderProgramHandle, "ambientColor");
        }


        // Create the buffers to store the geometry information
        private void CreateBuffers()
        {
            foreach (var obj in _objects)
            {
                obj.rotationMatrixHandle = GL.GetUniformLocation(_shaderProgramHandle, "rotationMatrix");
                obj.translationMatrixHandle = GL.GetUniformLocation(_shaderProgramHandle, "translationMatrix");
                obj.scaleMatrixHandle = GL.GetUniformLocation(_shaderProgramHandle, "scaleMatrix");
                obj.t1MatrixHandle = GL.GetUniformLocation(_shaderProgramHandle, "T1Matrix");
                obj.t2MatrixHandle = GL.GetUniformLocation(_shaderProgramHandle, "T2Matrix");

                //Vertex coords:
                var vertexPositionBufferHandle = 0;
                GL.GenBuffers(1, out vertexPositionBufferHandle);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vertexPositionBufferHandle);
                GL.BufferData(BufferTarget.ArrayBuffer,
                    new IntPtr(obj.VertexPositionData.Length * Vector3.SizeInBytes),
                    obj.VertexPositionData, BufferUsageHint.StaticDraw);
                obj.VertexPositionBufferHandle = vertexPositionBufferHandle;

                //Vertex Normals:
                var vertexNormalBufferHandle = 0;
                GL.GenBuffers(1, out vertexNormalBufferHandle);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vertexNormalBufferHandle);
                GL.BufferData(BufferTarget.ArrayBuffer,
                    new IntPtr(obj.VertexNormalData.Length * Vector3.SizeInBytes),
                    obj.VertexNormalData, BufferUsageHint.StaticDraw);
                obj.VertexNormalBufferHandle = vertexNormalBufferHandle;

                //Faces buffer.:
                var facesBufferHandle = 0;
                GL.GenBuffers(1, out facesBufferHandle);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, facesBufferHandle);
                GL.BufferData(BufferTarget.ElementArrayBuffer,
                    new IntPtr(sizeof(uint) * obj.FacesData.Length),
                    obj.FacesData, BufferUsageHint.StaticDraw);
                obj.FacesBufferHandle = facesBufferHandle;

                //Vertex texture coords:
                var textureBufferHandle = 0;
                GL.GenBuffers(1, out textureBufferHandle);
                GL.BindBuffer(BufferTarget.ArrayBuffer, textureBufferHandle);
                GL.BufferData(BufferTarget.ArrayBuffer,
                    new IntPtr(obj.VertexTextureData.Length * Vector3.SizeInBytes),
                    obj.VertexTextureData, BufferUsageHint.StaticDraw);
                obj.VertexTextureBufferHandle = textureBufferHandle;
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }


        //OpenGL has the concept of "Vertex Array Objects (VAO)" which are groups of geometries,
        //shaders and textures that are associated to one object that wants to be drawn
        //The idea of a VAO is to associate to one single handle a group of configurations
        //that tell the GPU which geometry we want to render, with what shader and what textures
        private void CreateVertexArrays()
        {
            foreach (var obj in _objects)
            {
                var objectHandle = 0;
                GL.GenVertexArrays(1, out objectHandle);
                GL.BindVertexArray(objectHandle);

                //Vertex coords:
                GL.EnableVertexAttribArray(0);
                GL.BindBuffer(BufferTarget.ArrayBuffer, obj.VertexPositionBufferHandle);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, true, Vector3.SizeInBytes, 0);
                GL.BindAttribLocation(_shaderProgramHandle, 0, "inPosition");

                //Vertex Normals:
                GL.EnableVertexAttribArray(1);
                GL.BindBuffer(BufferTarget.ArrayBuffer, obj.VertexNormalBufferHandle);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, true, Vector3.SizeInBytes, 0);
                GL.BindAttribLocation(_shaderProgramHandle, 1, "inNormal");

                //Vertex texture coords:
                GL.EnableVertexAttribArray(2);
                GL.BindBuffer(BufferTarget.ArrayBuffer, obj.VertexTextureBufferHandle);
                GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, true, Vector3.SizeInBytes, 0);
                GL.BindAttribLocation(_shaderProgramHandle, 2, "inUV");

                //Faces indices and obj handle for vao:
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, obj.FacesBufferHandle);
                obj.ObjectHandle = objectHandle;
            }
            GL.BindVertexArray(0);
        }


        //Creates a texture in GPU memory, and copies the bitmap image into it. 
        //The GPU has N texture units available (usually 8), so we need to assign this texture to one of the units
        private int CreateTexture(Bitmap texture, TextureUnit unit, TextureMinFilter minFilter, TextureMagFilter magFilter)
        {
            int textureId = GL.GenTexture();
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texture.Width, texture.Height, 0,
                            PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);

            var bmpData = texture.LockBits(new Rectangle(0, 0, texture.Width, texture.Height),
              ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, texture.Width, texture.Height, PixelFormat.Bgra,
              PixelType.UnsignedByte, bmpData.Scan0);

            texture.UnlockBits(bmpData);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            return textureId;
        }


        // The render method will be called once per frame erasing the previous image and generating a new one:
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            //Deltatime deberia quedar en segundos:
            long newCurrentTime = DateTime.Now.Ticks;
            deltaTime = (float)(newCurrentTime - currentTime) / (TimeSpan.TicksPerSecond) ;
            update(deltaTime);

            //Updates de variables uniforms:
            _cameraPosition = new float[] { scene.Camera.position.x, scene.Camera.position.y, scene.Camera.position.z };

            GL.Viewport(0, 0, this.Width, this.Height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // We need to indicate which shader to use before the drawing, and before sending uniform data:
            GL.UseProgram(_shaderProgramHandle);

            GL.Uniform3(_cameraPositionHandle, 1, _cameraPosition);
            GL.Uniform3(_lightPositionArrayHandle, (_lightPositionArray.Length / 3), _lightPositionArray);
            GL.Uniform3(_lightColorArrayHandle, (_lightColorArray.Length / 3), _lightColorArray);
            GL.Uniform3(_ambient_handle, 1, _ambientLight);
            GL.Uniform1(_lightCountHandle, (scene.Lights.Count - 1));

            foreach (var obj in _objects)
            {
                if (obj.GetType() == typeof(Player) && camera_mode == 0 && !camera_switching)
                    continue; 

                //We indicate that we will use texture unit 0 for this drawing, and bind it to our previously created texture
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, obj.TextureId);

                GL.UniformMatrix4(obj.rotationMatrixHandle, 1, false, obj.RotationMatrix);
                GL.UniformMatrix4(obj.translationMatrixHandle, 1, false, obj.TranslationMatrix);
                GL.UniformMatrix4(obj.scaleMatrixHandle, 1, false, obj.ScaleMatrix);
                GL.UniformMatrix4(obj.t1MatrixHandle, 1, false, obj.T1Matrix);//------------------------------€€€€
                GL.UniformMatrix4(obj.t2MatrixHandle, 1, false, obj.T2Matrix);//------------------------------€€€€
                GL.UniformMatrix4(_transformationMatrixHandle, 1, false, obj.TransformationMatrix);
                GL.Uniform1(_textureHandle, 0);
                GL.Uniform3(_materialColorHandle, (obj.MaterialColor.Length / 3), obj.MaterialColor);
                GL.Uniform1(_shininess_handle, obj.shininess);

                //We bind to our object handle so the GPU knows which geometries to draw:
                GL.BindVertexArray(obj.ObjectHandle);

                //Tells the GPU to draw what type of geometry with the faces/vertex data already stored in the GPU buffers:
                GL.DrawElements(BeginMode.Triangles, obj.FacesData.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);
            }

            currentTime = DateTime.Now.Ticks;
            SwapBuffers();
        }


        //Updates the game once per frame:
        public void update(float delta)
        {
            float[] transformationMatrix = get_transformation_matrix(SceneWidth, SceneHeight);
            tuple_3 w = Vectores.Normalize(scene.Camera.position - scene.Camera.target);
            tuple_3 u = Vectores.Normalize(Vectores.cross(scene.Camera.up, w));
            tuple_3 f = Vectores.cross(floor_normal, u);

            #region Salto:
            if (player.jumping)
            {
                //Se obtiene un vector que representa el desplazamiento horizontal de la camara (tgt y pos) y del player (face vector y translation matrix):
                jump_time += delta;
                tuple_3 z_mask = new tuple_3(0, 0, 1);
                z_mask = (jump_velocity * jump_time - (0.5f * g * (float)Math.Pow(jump_time, 2))) * z_mask;

                //Se desplazan en esa cantidad ambos actores:
                matrix_4 tm = new matrix_4(1,0,0,0,   0,1,0,0,   0,0,1,0,   z_mask.x,z_mask.y,z_mask.z,1);
                player.TranslationMatrix = matrix4_to_array(tm * array_to_matrix4(player.TranslationMatrix));
                player.face_vector += z_mask;
                scene.Camera.position += z_mask;
                scene.Camera.target += z_mask;

                //Obtengo nueva posicion del player
                tuple_4 pc = new tuple_4(player.center.X, player.center.Y, player.center.Z, 1);
                pc = array_to_rowMatrix4(player.TranslationMatrix) * pc;
                tuple_3 playerNewPos = new tuple_3(pc.x, pc.y, pc.z);

                //Ahora vendria la correccion de altura en caso de que queden mas abajo de lo que deben:
                if (playerNewPos.z < player.min_z)
                {
                    float dta = player.min_z - playerNewPos.z;
                    matrix_4 tm2 = new matrix_4(1,0,0,0,   0,1,0,0,   0,0,1,0,   0,0,dta,1);
                    player.TranslationMatrix = matrix4_to_array(tm2 * array_to_matrix4(player.TranslationMatrix));
                }
                if (scene.Camera.position.z < min_z)
                {
                    float dta = min_z - scene.Camera.position.z;
                    z_mask = dta * new tuple_3(0, 0, 1);
                    player.face_vector += z_mask;
                    scene.Camera.position += z_mask;
                    scene.Camera.target += z_mask;

                    jump_time = 0;
                    player.jumping = false;
                }
            }
            #endregion

            //Para que player se mueva solo:
            player.TransformationMatrix = transformationMatrix;

            //Objetos estaticos:
            foreach (var obj in _static_obj)
                obj.TransformationMatrix = transformationMatrix;

            //Animacion:
            foreach (var obj in _animated_obj)
            {
                obj.TransformationMatrix = transformationMatrix;
                //Rotacion en torno a su mismo eje:
                matrix_4 t1 = new matrix_4(1, 0, 0, -obj.center.X, 0, 1, 0, -obj.center.Y, 0, 0, 1, -obj.center.Z, 0, 0, 0, 1);
                matrix_4 t2 = new matrix_4(1, 0, 0, obj.center.X, 0, 1, 0, obj.center.Y, 0, 0, 1, obj.center.Z, 0, 0, 0, 1);
                //matrix_4 t2 = Matrix.inverse(t1);
                obj.RotationMatrix = colMatrix4_to_array(t2 * getEulerAngleMatrix(rotation_angle, 0, 0) * t1 * array_to_rowMatrix4(obj.RotationMatrix));
                #region Rotacion en torno a los ejes cardinales:
                //obj.RotationMatrix = matrix4_to_array(getEulerAngleMatrix(rotation_angle, 0, 0) * array_to_matrix4(obj.RotationMatrix));

                //Traslacion:
                /*matrix_4 tm = new matrix_4(1, 0, 0, 0,
                                           0, 1, 0, 0,
                                           0, 0, 1, 0,
                                           (float)Math.Sin((float)DegreeToRadian(rotation_angle)), 0, 0, 1);
                obj.TranslationMatrix = matrix4_to_array(tm * array_to_matrix4(obj.TranslationMatrix));//*/

                //Escalamiento:
                /*obj.ScaleMatrix = matrix4_to_array(new matrix_4(2, 0, 0, 0, 
                                                                0, 1, 0, 0, 
                                                                0, 0, 1, 0, 
                                                                0, 0, 0, 1));//*/
                #endregion
            }

            //Animacion de movimiento de camara:
            if (camera_switching)
            {
                scene.Camera.position += delta_pos;
                scene.Camera.target += delta_tgt;
                camera_frames--;

                if (camera_frames == 0)
                    camera_switching = false;
            }
        }


        [STAThread]//--------------------------------------------------------------------------------------------------------------------------
        public static void Main()
        {
            using (var window = new MainWindow(SceneWidth, SceneHeight))
            {
                window.Run(FrameRate);
            }
        }
        

        //Por ahora solo para cambiar rasterMode. Despues sera para controlar escena:
        private void KeyDown(object sender, KeyboardKeyEventArgs e)
        {
            tuple_3 w = Vectores.Normalize(scene.Camera.position - scene.Camera.target);
            tuple_3 u = Vectores.Normalize(Vectores.cross(scene.Camera.up, w));
            tuple_3 v = Vectores.Normalize(Vectores.cross(w, u));
            tuple_3 f = Vectores.Normalize(Vectores.cross(floor_normal, u));

            if (e.Key == Key.Space && !player.jumping)
            {
                this.min_z = scene.Camera.position.z;
                player.jumping = true;
                player.min_z = this.player.center.Z;
            }

            if (e.Key == Key.Tab && !camera_switching && !player.jumping)
            {
                camera_switching = true;
                camera_frames = 30;

                //Pasamos a 3ra persona
                if (camera_mode == 0)
                {
                    camera_mode = 1;

                    float T = 200;
                    tuple_3 oldPos = scene.Camera.position;
                    tuple_4 pc = new tuple_4(player.center.X, player.center.Y, player.center.Z, 1);
                    pc = array_to_rowMatrix4(player.TranslationMatrix) * pc;
                    tuple_3 playerTransPos = new tuple_3(pc.x, pc.y, pc.z);
                    tuple_3 addPos = - (T * f) + ((T / 2f) * floor_normal);
                    tuple_3 addTgt = ((oldPos + addPos) + (playerTransPos - (oldPos + addPos))) - scene.Camera.target;

                    delta_pos = addPos / camera_frames;
                    delta_tgt = addTgt / camera_frames;

                    //Vamos a suponer que los vectores salen de la camara:
                    player.fp_camPos = playerTransPos;
                    player.front_pos =  f;
                    player.back_pos  = -f;
                    player.right_pos =  u;
                    player.left_pos  = -u;
                }
                //Volvemos a primera persona
                else
                {
                    camera_mode = 0;
                    
                    tuple_3 d1 = scene.Camera.target - scene.Camera.position;
                    tuple_3 d2 = player.face_vector - scene.Camera.target;
                    delta_pos = new tuple_3(d1.x, d1.y, d1.z) / camera_frames;
                    delta_tgt = new tuple_3(d2.x, d2.y, d2.z);
                }
            }

            if (e.Key == Key.Escape)
            {
                Exit();
            }
        }

        
        //Por ahora solo para cambiar rasterMode. Despues sera para controlar escena:
        private void KeyUp(object sender, KeyboardKeyEventArgs e) 
        {
            if (e.Key == Key.A || e.Key == Key.D || e.Key == Key.W || e.Key == Key.S)
                player.moving = false;
        }


        //Por ahora solo para cambiar rasterMode. Despues sera para controlar escena:
        private void clicked(object sender, MouseButtonEventArgs e)
        {
            mouseClicked = true;
            if (!player.jumping && camera_mode == 0)
            {
                this.min_z = scene.Camera.position.z;
                player.jumping = true;
                player.min_z = this.player.center.Z;
            }
        }


        //Por ahora solo para cambiar rasterMode. Despues sera para controlar escena:
        private void unclicked(object sender, MouseButtonEventArgs e)
        {
            mouseClicked = false;
        }


        //Por ahora solo para cambiar rasterMode. Despues sera para controlar escena:
        private void KeyPressed(object sender, KeyPressEventArgs e)
        {
            tuple_3 w = Vectores.Normalize(scene.Camera.position - scene.Camera.target);
            tuple_3 u = Vectores.Normalize(Vectores.cross(scene.Camera.up, w));
            tuple_3 f = Vectores.cross(floor_normal, u);
            tuple_3 playerPos = scene.Camera.target;

            player.front_pos = f;
            player.back_pos = -f;
            player.right_pos = u;
            player.left_pos = -u;
            
            float forwardSpeed = 2.5f;
            float sideSpeed = 2.5f;

            matrix_4 t1 = new matrix_4(1, 0, 0, -player.center.X, 0, 1, 0, -player.center.Y, 0, 0, 1, -player.center.Z, 0, 0, 0, 1);
            matrix_4 t2 = new matrix_4(1, 0, 0,  player.center.X, 0, 1, 0,  player.center.Y, 0, 0, 1,  player.center.Z, 0, 0, 0, 1);
            tuple_3 v1 = Vectores.Normalize(player.face_vector - playerPos);

            if (e.KeyChar == 'a' && !camera_switching) 
            {
                float left_angle = RadianToDegree((float)Math.Acos(Vectores.dot(v1, player.left_pos)));
                tuple_3 v3 = Vectores.cross(v1, player.left_pos);
                float sign = Vectores.dot(v3, floor_normal);
                matrix_4 left_RvOv = new matrix_4();
                if (sign < 0)
                    left_RvOv = getEulerAngleMatrix(-left_angle, 0, 0);
                else
                    left_RvOv = getEulerAngleMatrix(+left_angle, 0, 0);
                float distance = Vectores.dist(Vectores.Normalize(player.face_vector - scene.Camera.target), player.left_pos);

                if (camera_mode == 1 && distance > 0.001f)
                {
                    player.RotationMatrix = colMatrix4_to_array(t2 * left_RvOv * t1 * array_to_rowMatrix4(player.RotationMatrix));
                    player.face_vector = scene.Camera.target + player.left_pos;
                }
                if (distance <= 0.001f || camera_mode == 0)
                {
                    scene.Camera.position -= sideSpeed * u;
                    scene.Camera.target -= sideSpeed * u;
                    player.face_vector -= sideSpeed * u;
                    matrix_4 translation = new matrix_4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -sideSpeed * u.x, -sideSpeed * u.y, 0, 1);
                    player.TranslationMatrix = matrix4_to_array(translation * array_to_matrix4(player.TranslationMatrix));
                }
            }
            if (e.KeyChar == 'd' && !camera_switching)
            {
                float right_angle = RadianToDegree((float)Math.Acos(Vectores.dot(v1, player.right_pos)));
                tuple_3 v3 = Vectores.cross(v1, player.right_pos);
                float sign = Vectores.dot(v3, floor_normal);
                matrix_4 right_RvOv = new matrix_4();
                if (sign < 0)
                    right_RvOv = getEulerAngleMatrix(-right_angle, 0, 0);
                else
                    right_RvOv = getEulerAngleMatrix(+right_angle, 0, 0);
                float distance = Vectores.dist(Vectores.Normalize(player.face_vector - scene.Camera.target), player.right_pos);

                if (camera_mode == 1 && distance > 0.001f)
                {
                    player.RotationMatrix = colMatrix4_to_array(t2 * right_RvOv * t1 * array_to_rowMatrix4(player.RotationMatrix));
                    player.face_vector = scene.Camera.target + player.right_pos;
                }
                if (distance <= 0.001f || camera_mode == 0)
                {
                    scene.Camera.position += sideSpeed * u;
                    scene.Camera.target += sideSpeed * u;
                    player.face_vector += sideSpeed * u;
                    matrix_4 translation = new matrix_4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, sideSpeed * u.x, sideSpeed * u.y, 0, 1);
                    player.TranslationMatrix = matrix4_to_array(translation * array_to_matrix4(player.TranslationMatrix));
                }
            }
            if (e.KeyChar == 'w' && !camera_switching)
            {
                float front_angle = RadianToDegree((float)Math.Acos(Vectores.dot(v1, player.front_pos)));
                tuple_3 v3 = Vectores.cross(v1, player.front_pos);
                float sign = Vectores.dot(v3, floor_normal);
                matrix_4 front_RvOv = new matrix_4();
                if (sign < 0)   
                    front_RvOv = getEulerAngleMatrix(-front_angle, 0, 0);
                else
                    front_RvOv = getEulerAngleMatrix(+front_angle, 0, 0);

                float distance = Vectores.dist(Vectores.Normalize(player.face_vector - scene.Camera.target), player.front_pos);

                if (camera_mode == 1 && distance > 0.001f)
                {
                    player.RotationMatrix = colMatrix4_to_array(t2 * front_RvOv * t1 * array_to_rowMatrix4(player.RotationMatrix));
                    player.face_vector = scene.Camera.target + player.front_pos;
                }
                if (distance <= 0.001f || camera_mode == 0)
                {
                    scene.Camera.position += forwardSpeed * f;
                    scene.Camera.target += forwardSpeed * f;
                    player.face_vector += forwardSpeed * f;
                    matrix_4 translation = new matrix_4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, forwardSpeed * f.x, forwardSpeed * f.y, 0, 1);
                    player.TranslationMatrix = matrix4_to_array(translation * array_to_matrix4(player.TranslationMatrix)); 
                }
            }
            if (e.KeyChar == 's' && !camera_switching)
            {
                float back_angle = RadianToDegree((float)Math.Acos(Vectores.dot(v1, player.back_pos)));
                tuple_3 v3 = Vectores.cross(v1, player.back_pos);
                float sign = Vectores.dot(v3, floor_normal);
                matrix_4 back_RvOv = new matrix_4();
                if (sign < 0)
                    back_RvOv = getEulerAngleMatrix(-back_angle, 0, 0);
                else
                    back_RvOv = getEulerAngleMatrix(+back_angle, 0, 0);
                float distance = Vectores.dist(Vectores.Normalize(player.face_vector - scene.Camera.target), player.back_pos);

                if (camera_mode == 1 && distance > 0.001f)
                {
                    player.RotationMatrix = colMatrix4_to_array(t2 * back_RvOv * t1 * array_to_rowMatrix4(player.RotationMatrix));
                    player.face_vector = scene.Camera.target + player.back_pos;
                }
                if (distance <= 0.001f || camera_mode == 0)
                {
                    scene.Camera.position -= forwardSpeed * f;
                    scene.Camera.target -= forwardSpeed * f;
                    player.face_vector -= forwardSpeed * f;
                    matrix_4 translation = new matrix_4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -forwardSpeed * f.x, -forwardSpeed * f.y, 0, 1);
                    player.TranslationMatrix = matrix4_to_array(translation * array_to_matrix4(player.TranslationMatrix));
                }
            }
        }


        //Veamos si funciona!
        private void mouseMoved(object sender, MouseMoveEventArgs e)
        {
            tuple_3 w = Vectores.Normalize(scene.Camera.position - scene.Camera.target);
            tuple_3 u = Vectores.Normalize(Vectores.cross(scene.Camera.up, w));
            tuple_3 v = Vectores.Normalize(Vectores.cross(w, u));
            float sensitivity = 0.4f;

            //Movimiento de camara en primera persona usando el mouse:
            if (!camera_switching && !player.jumping && camera_mode == 0)
            {
                float delta = 2;
                if (mousePrevX > delta && mousePrevX < (SceneWidth - delta) && mousePrevY > delta && mousePrevY < (SceneHeight - delta))
                {
                    //Obtiene las matrices de rotacion:
                    tuple_3 cPos = scene.Camera.position;
                    matrix_4 RuOu = getAxisAngleMatrix(u, 1, -e.YDelta * sensitivity); 
                    matrix_4 RvOv = getAxisAngleMatrix(floor_normal, 1, -e.XDelta * sensitivity); //Alternativa v
                    matrix_4 T1 = new matrix_4(1, 0, 0, -cPos.x, 0, 1, 0, -cPos.y, 0, 0, 1, -cPos.z, 0, 0, 0, 1.0f);
                    matrix_4 T2 = new matrix_4(1, 0, 0,  cPos.x, 0, 1, 0,  cPos.y, 0, 0, 1,  cPos.z, 0, 0, 0, 1.0f);
                    matrix_4 TRRT = T2 * RvOv * RuOu * T1;

                    //Rotacion del player junto a la camara:
                    matrix_4 TP1 = new matrix_4(1,0,0,-player.center.X,   0,1,0,-player.center.Y,   0,0,1,-player.center.Z,   0,0,0,1);
                    matrix_4 TP2 = new matrix_4(1,0,0, player.center.X,   0,1,0, player.center.Y,   0,0,1, player.center.Z,   0,0,0,1);
                    player.RotationMatrix = colMatrix4_to_array(TP2 * RvOv * TP1 * array_to_rowMatrix4(player.RotationMatrix));
                    //player.T1Matrix = colMatrix4_to_array(TP2); 
                    //player.T2Matrix = colMatrix4_to_array(TP1);
                    //player.RotationMatrix = colMatrix4_to_array(RvOv * array_to_rowMatrix4(player.RotationMatrix));

                    //Modifica el target usando las matrices de rotacion:
                    tuple_4 ti = new tuple_4(scene.Camera.target.x, scene.Camera.target.y, scene.Camera.target.z, 1.0f);
                    tuple_4 tf = TRRT * ti;
                    scene.Camera.target = new tuple_3(tf.x, tf.y, tf.z);

                    //Actualiza valor de frente del player para saber siempre a donde esta mirando:
                    tuple_4 t4fv = T2 * RvOv * T1 * (new tuple_4(player.face_vector, 1));
                    player.face_vector = new tuple_3(t4fv.x, t4fv.y, t4fv.z);
                }
                mousePrevX = e.X;
                mousePrevY = e.Y;
            }
            //Movimiento de camara en tercera persona usando el mouse:
            else if (!camera_switching && mouseClicked && camera_mode == 1 && !player.jumping)
            {
                tuple_3 cTgt = scene.Camera.target;
                tuple_3 cPos = scene.Camera.position;

                matrix_4 RuOu = getAxisAngleMatrix(u, 1, -e.YDelta * sensitivity);
                matrix_4 RvOv = getAxisAngleMatrix(floor_normal, 1, -e.XDelta * sensitivity);
                matrix_4 T1 = new matrix_4(1, 0, 0, -cTgt.x, 0, 1, 0, -cTgt.y, 0, 0, 1, -cTgt.z, 0, 0, 0, 1.0f);
                matrix_4 T2 = new matrix_4(1, 0, 0,  cTgt.x, 0, 1, 0,  cTgt.y, 0, 0, 1,  cTgt.z, 0, 0, 0, 1.0f); 
                matrix_4 TRRT = T2 * RvOv * RuOu * T1;
                
                tuple_4 ei = new tuple_4(cPos.x, cPos.y, cPos.z, 1.0f);
                tuple_4 ef = TRRT * ei;
                scene.Camera.position = new tuple_3(ef.x, ef.y, ef.z);
            }
        }


        //Para hacer zoom in y out usando la ruedita del mouse:
        private void wheelchanged(object sender, MouseWheelEventArgs e)
        {
            if (camera_mode == 0) 
            {
                rotation_angle += (e.DeltaPrecise);
                if (rotation_angle >= 360)
                    rotation_angle = rotation_angle - 360; 
            }
            else
            {
                //Se calcula el vector direccion desde la camara al player para hacer el zoom in y zoom out:
                float speed = 0.05f;
                tuple_4 pc = new tuple_4(player.center.X, player.center.Y, player.center.Z, 1);
                pc = array_to_rowMatrix4(player.TranslationMatrix) * pc;
                tuple_3 f = (new tuple_3(pc.x, pc.y, pc.z) - scene.Camera.position);
                float distance = (float)Math.Sqrt(Math.Pow(f.x,2f) + Math.Pow(f.y, 2f) + Math.Pow(f.z, 2f));
                
                //50 sera lo mas cerca que quedara al player-
                if (e.DeltaPrecise > 0.0f && distance >= 50) 
                    scene.Camera.position += speed * e.DeltaPrecise * f;
                //500 sera lo mas que se alejara la camara del player-
                else if (e.DeltaPrecise < 0.0f && distance <= 500) 
                    scene.Camera.position += speed * e.DeltaPrecise * f;
            }
        }


        //Retorna matriz de rotacion usando metodo AXIS-ANGLE.
        private matrix_4 getAxisAngleMatrix(tuple_3 a, float near, float coord)
        {
            float tan = (float)Math.Tan(DegreeToRadian(coord / near));
            float angle = (float)Math.Atan(tan);
            float c = (float)Math.Cos(angle);
            float s = tan * c;
            float c2 = 1 - c;

            return new matrix_4(a.x * a.x * c2 + c, a.x * a.y * c2 - a.z * s, a.x * a.z * c2 + a.y * s, 0.0f,
                                a.y * a.x * c2 + a.z * s, a.y * a.y * c2 + c, a.y * a.z * c2 - a.x * s, 0.0f,
                                a.z * a.x * c2 - a.y * s, a.z * a.y * c2 + a.x * s, a.z * a.z * c2 + c, 0.0f,
                                0.0f, 0.0f, 0.0f, 1.0f);
        }


        //Retorna matriz de rotacion usando metodo AXIS-ANGLE.--------#############
        private matrix_4 getEulerAngleMatrix(float yaw, float pitch, float roll)
        {
            float ysin = (float)Math.Sin(DegreeToRadian(yaw));
            float ycos = (float)Math.Cos(DegreeToRadian(yaw));
            float psin = (float)Math.Sin(DegreeToRadian(pitch));
            float pcos = (float)Math.Cos(DegreeToRadian(pitch));
            float rsin = (float)Math.Sin(DegreeToRadian(roll));
            float rcos = (float)Math.Cos(DegreeToRadian(roll));

            matrix_4 x_rot = new matrix_4(  1.0f, 0.0f, 0.0f, 0.0f,
                                            0.0f, pcos, -psin, 0.0f,
                                            0.0f, psin, pcos, 0.0f,
                                            0.0f, 0.0f, 0.0f, 1.0f);

            matrix_4 y_rot = new matrix_4(  rcos, 0.0f, rsin, 0.0f,
                                            0.0f, 1.0f, 0.0f, 0.0f,
                                            -rsin, 0.0f, rcos, 0.0f,
                                            0.0f, 0.0f, 0.0f, 1.0f);

            matrix_4 z_rot = new matrix_4(  ycos, -ysin, 0.0f, 0.0f,
                                            ysin, ycos, 0.0f, 0.0f,
                                            0.0f, 0.0f, 1.0f, 0.0f,
                                            0.0f, 0.0f, 0.0f, 1.0f);

            return z_rot * y_rot * x_rot;
        }


        //Para no tener todo el codigo molestando arriba:
        public static float[] get_transformation_matrix(int width, int height)
        {
            Vector3 e = new Vector3(scene.Camera.position.x, scene.Camera.position.y, scene.Camera.position.z);
            Vector3 t = new Vector3(scene.Camera.target.x, scene.Camera.target.y, scene.Camera.target.z);
            Vector3 up = new Vector3(scene.Camera.up.x, scene.Camera.up.y, scene.Camera.up.z);

            float N = -scene.Camera.near;
            float F = -scene.Camera.far;

            float top = scene.Camera.near * (float)Math.Tan(Scene.DegreeToRadian(scene.Camera.FOV / 2.0f));
            float bottom = -top;
            float right = ((float)width / height) * (top);
            float left = -right;

            Vector3 w = Vector3.Normalize(e - t);
            Vector3 u = Vector3.Normalize(Vector3.Cross(up, w));
            Vector3 v = Vector3.Normalize(Vector3.Cross(w, u));

            //Obtengo la matriz de transformacion desde espacio mundo a espacio camara:
            Matrix4 C = new Matrix4(u.X, v.X, w.X, e.X,
                                     u.Y, v.Y, w.Y, e.Y,
                                     u.Z, v.Z, w.Z, e.Z,
                                     0.0f, 0.0f, 0.0f, 1.0f);
            C = Matrix4.Invert(C);

            //Ahora obtengo matriz para perspectiva:
            float a = (N + F) / N;
            float b = -F;
            float c = 1.0f / N;
            Matrix4 S = new Matrix4(1, 0, 0, 0,
                                     0, 1, 0, 0,
                                     0, 0, a, b,
                                     0, 0, c, 0);

            //Ahora hago la transformacion desde espacio camara a espacio proyeccion:
            float riLe = (2 / (right - left));
            float toBo = (2 / (top - bottom));
            float neFa = (2 / (N - F));
            Matrix4 P1 = new Matrix4(1.0f, 0.0f, 0.0f, -left,
                                      0.0f, 1.0f, 0.0f, -bottom,
                                      0.0f, 0.0f, 1.0f, -F,
                                      0.0f, 0.0f, 0.0f, 1.0f);
            Matrix4 P2 = new Matrix4(riLe, 0.0f, 0.0f, 0.0f,
                                      0.0f, toBo, 0.0f, 0.0f,
                                      0.0f, 0.0f, neFa, 0.0f,
                                      0.0f, 0.0f, 0.0f, 1.0f);
            Matrix4 P3 = new Matrix4(1.0f, 0.0f, 0.0f, -1.0f,
                                      0.0f, 1.0f, 0.0f, -1.0f,
                                      0.0f, 0.0f, 1.0f, -1.0f,
                                      0.0f, 0.0f, 0.0f, 1.0f);

            Matrix4 P = P3 * P2 * P1;

            Matrix4 PSC = Matrix4.Mult(Matrix4.Mult(P, S), C);

            return new float[] {PSC.Column0.X, PSC.Column0.Y, PSC.Column0.Z, PSC.Column0.W,
                                PSC.Column1.X, PSC.Column1.Y, PSC.Column1.Z, PSC.Column1.W,
                                PSC.Column2.X, PSC.Column2.Y, PSC.Column2.Z, PSC.Column2.W,
                                PSC.Column3.X, PSC.Column3.Y, PSC.Column3.Z, PSC.Column3.W};
        }


        //Convierte de Grados a Radianes.
        public static float DegreeToRadian(float angle)
        {
            return (float)(angle * (Math.PI / 180.0f));
        }


        //Convierte de Radianes a Grados.
        public static float RadianToDegree(float rad)
        {
            return (float)(rad * (180f / Math.PI));
        }


        //Para guardar colores de materiales en el uniform:
        public int set_material_handle(Mesh obj, ref float[] mat_color)
        {
            int textureId = 0;
            mat_color = new float[6];
            foreach (Material mat in obj.materials)
            {
                if (!mat.use_tex)
                {
                    if (mat.material_type == "lambert")
                    {
                        mat_color[0] = mat.color[0];
                        mat_color[1] = mat.color[1];
                        mat_color[2] = mat.color[2];
                    }
                    else
                    {
                        mat_color[3] = mat.color[0];
                        mat_color[4] = mat.color[1];
                        mat_color[5] = mat.color[2];
                    }
                }
                else
                {
                    Material_brdf_textured matex = (Material_brdf_textured)mat;
                    textureId = CreateTexture(matex.bitmaps[matex.color_texture], TextureUnit.Texture0, TextureMinFilter.Nearest, TextureMagFilter.Linear);
                }
            }
            return textureId;
        }


        //Para obtener shininess y guardar en uniform:
        public void set_material_shininess(Mesh obj, Object new_obj)
        {
            foreach (Material mat in obj.materials)
            {
                if (!(mat.material_type == "lambert"))
                {
                    if (mat.material_type == "blinnPhong")
                    {
                        Material_brdf mat2 = (Material_brdf)mat;
                        new_obj.shininess = Convert.ToInt32(mat2.brdfParams["shininess"]);
                    }
                    else
                    {
                        Material_brdf_textured mat2 = (Material_brdf_textured)mat;
                        new_obj.shininess = Convert.ToInt32(mat2.brdfParams["shininess"]);
                    }
                }
            }
        }
        
        
        //Transforma de array de floats a m_4:
        public matrix_4 array_to_matrix4(float[] a) 
        {
            return new matrix_4(a[0], a[1], a[2], a[3],
                                a[4], a[5], a[6], a[7],
                                a[8], a[9], a[10], a[11],
                                a[12], a[13], a[14], a[15]);
        }

        
        //Transforma de array de floats a m_4 (en modo fila).
        public matrix_4 array_to_rowMatrix4(float[] a)
        {
            return new matrix_4(a[0], a[4], a[08], a[12],
                                a[1], a[5], a[09], a[13],
                                a[2], a[6], a[10], a[14],
                                a[3], a[7], a[11], a[15]);
        }


        //Transforma de m_4 a array de floats:     
        public float[] matrix4_to_array(matrix_4 a)
        {
            return new float[] { a.a00, a.a01, a.a02, a.a03, a.a10, a.a11, a.a12, a.a13,
                                 a.a20, a.a21, a.a22, a.a23, a.a30, a.a31, a.a32, a.a33};
        }


        //Transforma de m_4 (en modo columna) a array de floats:     
        public float[] colMatrix4_to_array(matrix_4 a)
        {
            return new float[] { a.a00, a.a10, a.a20, a.a30, 
                                 a.a01, a.a11, a.a21, a.a31,
                                 a.a02, a.a12, a.a22, a.a32, 
                                 a.a03, a.a13, a.a23, a.a33};
        }
    }
}
