using System;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Input;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Ejemplo2;

namespace Transformation
{ 

    //Clase que carga toda la escena y maneja el proceso de rendering.
    class Scene
    {
        public Dictionary<string, object> Parameters { get; set; }
        public Camera Camera { get; set; }
        public List<Mesh> Objects { get; set; }
        public List<Light> Lights { get; set; }
        public AmbientLight ambient_light { get; set; }
        public static Random rand = new Random(DateTime.Now.Millisecond); 


        //Constructor de Scene.
        Scene(Dictionary<string, object> parameters, Camera camera, List<Mesh> objects, List<Light> lights, AmbientLight ambient)//Dictionary<string, Light> lights)
        {
            Parameters = parameters;
            Camera = camera;
            Objects = objects;
            Lights = lights;
            ambient_light = ambient;
        }


        //Metodo recursivo que saca todos los datos de la escena y los guarda en scene.
        private static object ObjectHook(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var children = token.Children<JProperty>();
                    var dic = children.ToDictionary(prop => prop.Name, prop => ObjectHook(prop.Value));
          
                    if (dic.ContainsKey("__type__"))
                    {
                        if (dic["__type__"].ToString() == "scene")
                        {
                            AmbientLight ambient = null;
                            var camera = (Camera)dic["camera"];
                            var Lights = ((List<Object>)dic["lights"]).ConvertAll(x => (Light)x);
                            foreach (object l in (List<Object>)dic["lights"])
                            {
                                Light light = (Light)l;
                                if (light.name == "ambient_light")
                                {
                                    ambient = (AmbientLight)light;
                                    break;
                                }
                            }
                            var Objects = ((List<Object>)dic["objects"]).ConvertAll(x => (Mesh)x);
                            return new Scene((Dictionary<string, object>)dic["params"], camera, Objects, Lights, ambient);
                        }
                        else if (dic["__type__"].ToString() == "camera")
                        {
                            var fov = Convert.ToSingle(dic["fov"]);
                            var position = ((List<object>)dic["position"]).Select(Convert.ToSingle).ToList();
                            var up = ((List<object>)dic["up"]).Select(Convert.ToSingle).ToList();
                            var target = ((List<object>)dic["target"]).Select(Convert.ToSingle).ToList();

                            var near = -1.0f;
                            if (dic.ContainsKey("near"))
                                near = Convert.ToSingle(dic["near"]);
                            var far = -1.0f;
                            if (dic.ContainsKey("far"))
                                far = Convert.ToSingle(dic["far"]);

                            tuple_3 pos = new tuple_3 { x = position[0], y = position[1], z = position[2]};
                            tuple_3 cup = new tuple_3 { x = up[0], y = up[1], z = up[2] };
                            tuple_3 tgt = new tuple_3 { x = target[0], y = target[1], z = target[2] };

                            return new Camera(fov, pos, cup, tgt, near, far);
                        }
                        else if (dic["__type__"].ToString() == "mesh")
                        {
                            bool animate = false;
                            bool player = false;
                            var path = (string)dic["file_path"];
                            if (dic.ContainsKey("animate"))
                                animate = (bool)dic["animate"];
                            if (dic.ContainsKey("player"))
                                player = (bool)dic["player"];
                            var names = ((List<Object>)dic["materials"]).ConvertAll(x => (string)x);
                            List<Material> materials = new List<Material>();
                            foreach (string name in names)
                            {
                                materials.Add(Resources.materials[name]);
                            }

                            return new Mesh(path, materials, animate, player);
                        }
                        else if (dic["__type__"].ToString() == "point_light")
                        {
                            var position = ((List<object>)dic["position"]).Select(Convert.ToSingle).ToList();
                            var color = ((List<object>)dic["color"]).Select(Convert.ToSingle).ToList();
                            tuple_3 pos = new tuple_3 { x = position[0], y = position[1], z = position[2] };

                            return new PointLight(pos, color);
                        }
                        else if (dic["__type__"].ToString() == "ambient_light")
                        {
                            var color = ((List<object>)dic["color"]).Select(Convert.ToSingle).ToList();
                            return new AmbientLight(color);
                        }
                    }
                    return dic;

                case JTokenType.Array:
                    return token.Select(ObjectHook).ToList();
                default:
                    return ((JValue)token).Value;
            }
        }


        //Carga la escena completa y deja los pixeles listos para pintar.
        public static Scene LoadScene(string fileName, int width, int height)
        {
            try
            {
                //Recupero el json y guardo todos los valores en el objeto scene.
                var jsonString1 = File.ReadAllText(fileName);
                Resources.load();
                var scene = (Scene)ObjectHook(JToken.Parse(jsonString1));      
                return scene;
            }
            catch (IOException)
            {
                Console.WriteLine("Error, archivo no existe!");
                Console.Beep();
                return null;
            }
        }

        
        //Convierte de Angulo a Radianes.
        public static float DegreeToRadian(float angle)
        {
            return (float)Math.PI * angle / 180.0f;
        }


        //No obtiene determinante, sino uno de los elementos para sacarlo.
        public static float dt(float pond, float a, float b, float c, float d) 
        {
            float det_A = pond * (a * d - b * c);
            return det_A;
        }


        //Retorna float aleatorio entre ambos valores.
        public static float next_float(float min, float max)
        {
            float r = (float)rand.NextDouble();
            r *= (max - min);
            r += min;
            return r;
        }


        //Retorna vector 4 para transformaciones.
        public static tuple_4 to_v4(tuple_3 v)
        {
            return new tuple_4 { x = v.x, y = v.y, z = v.z, w = 1.0f};
        }
     
 
        //Retorna vector 3 para transformaciones.
        public static tuple_3 to_v3(tuple_4 v)
        {
            return new tuple_3 { x = v.x, y = v.y, z = v.z};
        }
    }   
}
