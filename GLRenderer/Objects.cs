using System;
using OpenTK;
using OpenTK.Input;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;

namespace Ejemplo2
{
    class Mesh 
    {
        public string path;
        public bool cvn = false;
        public bool vn = false;
        public bool vt = false;
        public bool animate;
        public bool player;
        public tuple_3 center;

        public Dictionary<int, tuple_3> vertex = new Dictionary<int, tuple_3>(); //LE PASAS EL INDICE DE LAS CARAS Y SACA EL VERTICE QUE CORRESPONDA
        public Dictionary<int, tuple_3> vt_dic = new Dictionary<int, tuple_3>(); //LE PASAS EL INDICE DE LAS CARAS Y SACA LA TUPLA DE VT QUE CORRESPONDA
        public Dictionary<int, tuple_3> vn_dic = new Dictionary<int, tuple_3>(); //LE PASAS EL INDICE DE LAS CARAS Y SACA LA NORMAL QUE CORRESPONDA (CVN FALSE)
        public Dictionary<int, tuple_3> vertex_normals = new Dictionary<int, tuple_3>(); //LE PASAS EL INDICE DE LAS CARAS Y SACA LA NORMAL QUE CORRESPONDA

        public Vector3[] vn_array;
        public Vector3[] vt_array;
        public List<tuple_3[]> faces = new List<tuple_3[]>(); //LISTA DE ARRAY DE TUPLAS. CADA ARRAY TIENE 3 TUPLAS. PARA VERTEX, VN, VT.
        public List<Material> materials;
        public float[] bounds = null;

        public Mesh(string path, List<Material> materials, bool animate, bool player) 
        {
            this.path = path;
            this.materials = materials;
            this.animate = animate;
            this.player = player;

            load_mesh();

            if (vn_dic.Count > 0) this.vn = true;
            else                  this.cvn = true;
            if (vt_dic.Count > 0) this.vt = true;    
            
            if (this.cvn)   
                compute_vertex_normals();
            if (vn || vt)
                rearrange_buffers();

            this.center = new tuple_3((bounds[0] + bounds[1]) / 2f, (bounds[2] + bounds[3]) / 2f, (bounds[4] + bounds[5]) / 2f);
        }

        private void load_mesh()
        {
            //Line guarda cada linea del texto
            string line;

            //Index es para saber a que vertice se refieren las caras.
            int index = 1;  int vt_index = 1;   int vn_index = 1; //DESPUES CAMBIAR PARA QUE PARTAN EN 0 COMO EN OPENGL.

            System.IO.StreamReader file = new System.IO.StreamReader("..\\..\\meshes\\" + path);
            while ((line = file.ReadLine()) != null) 
            {
                //Este fragmento de codigo es por si viene con mas de un espacio el .obj separando los datos. 
                string[] fragment = line.Split(' ');
                string[] frag = new string[4];
                int i = 0;
                foreach (string f in fragment)
                    if (f != "" && i < 4) frag[i++] = f;
                
                if (frag[0] == "v")
                {
                    save_vertex(frag[1], frag[2], frag[3], index);
                    index++;
                }
                else if (frag[0] == "#") continue;
                else if (frag[0] == "f")
                {
                    save_face(frag[1], frag[2], frag[3], cvn);
                }
                else if (frag[0] == "vt")
                {
                    save_vt(frag[1], frag[2], "0.0", vt_index);
                    vt_index++;
                }
                else if (frag[0] == "vn")
                {
                    save_vn(frag[1], frag[2], frag[3], vn_index);
                    vn_index++;
                }
                else { }
            }
            file.Close();    
        }

        //Reaordena el arreglo de normales y de uvs para que este ordenado de acuerdo al archivo.
        private void rearrange_buffers() 
        {
            if (vn)
                this.vn_array = new Vector3[vertex.Count];//vn_dic.Count]; 
            if (vt)
                this.vt_array = new Vector3[vertex.Count];//vt_dic.Count];

            foreach (tuple_3[] face in faces)
            {
                //Los indices de los vertices sera la posicion a la que deberemos llevar las normales y uvs.
                int v1_index = (int)face[0].x - 1;
                int v2_index = (int)face[0].y - 1;
                int v3_index = (int)face[0].z - 1;

                //Los indices de de donde hay que sacar el valor que ira en la posicion:
                int vn1_index = (int)face[2].x;
                int vn2_index = (int)face[2].y;
                int vn3_index = (int)face[2].z; 
                int vt1_index = (int)face[1].x;
                int vt2_index = (int)face[1].y;
                int vt3_index = (int)face[1].z;

                //Hago rearrange a vn solo en caso de ser verdadero:
                if (vn) 
                {
                    vn_array[v1_index] = new Vector3(vn_dic[vn1_index].x, vn_dic[vn1_index].y, vn_dic[vn1_index].z);
                    vn_array[v2_index] = new Vector3(vn_dic[vn2_index].x, vn_dic[vn2_index].y, vn_dic[vn2_index].z);
                    vn_array[v3_index] = new Vector3(vn_dic[vn3_index].x, vn_dic[vn3_index].y, vn_dic[vn3_index].z);
                }

                //Lo mismo aqui:
                if (vt)
                {
                    vt_array[v1_index] = new Vector3(vt_dic[vt1_index].x, vt_dic[vt1_index].y, vt_dic[vt1_index].z);
                    vt_array[v2_index] = new Vector3(vt_dic[vt2_index].x, vt_dic[vt2_index].y, vt_dic[vt2_index].z);
                    vt_array[v3_index] = new Vector3(vt_dic[vt3_index].x, vt_dic[vt3_index].y, vt_dic[vt3_index].z);
                }
            }
        }

        //Guarda vertices en diccionario que los relaciona  aun indice.
        private void save_vertex(string a, string b, string c, int index)
        {
            float x = float.Parse(a);
            float y = float.Parse(b);
            float z = float.Parse(c);
            vertex.Add(index, new tuple_3 { x = x, y = y, z = z });
            set_max_min(x, y, z);
        }

        //Define datos de max y min de cada coord.
        private void set_max_min(float x, float y, float z)
        {
            if (bounds == null)
                bounds = new float[] { x, x, y, y, z, z};
            else
            {
                if (x > bounds[0])
                    bounds[0] = x;
                if (x < bounds[1])
                    bounds[1] = x;
                if (y > bounds[2])
                    bounds[2] = y;
                if (y < bounds[3])
                    bounds[3] = y;
                if (z > bounds[4])
                    bounds[4] = z;
                if (z < bounds[5])
                    bounds[5] = z;
            }
        }

        //Guarda caras en listas. Mira primero si debe o no guardar vn y vt.
        private void save_face(string a, string b, string c, bool cvn)
        {
            tuple_3[] tuple_array = new tuple_3[3];
            if (a.Contains("/"))
            {
                string[] f1 = a.Split('/');
                string[] f2 = b.Split('/');
                string[] f3 = c.Split('/');

                for (int i = 0; i < 3; i++)
                {
                    if (f1[i] == "") f1[i] = "0.0";
                    if (f2[i] == "") f2[i] = "0.0";
                    if (f3[i] == "") f3[i] = "0.0";
                }

                tuple_array[0] = new tuple_3 { x = float.Parse(f1[0]), y = float.Parse(f2[0]), z = float.Parse(f3[0]) };
                tuple_array[1] = new tuple_3 { x = float.Parse(f1[1]), y = float.Parse(f2[1]), z = float.Parse(f3[1]) };
                tuple_array[2] = new tuple_3 { x = float.Parse(f1[2]), y = float.Parse(f2[2]), z = float.Parse(f3[2]) };
                faces.Add(tuple_array);
            }
            else
            {
                float x = float.Parse(a);
                float y = float.Parse(b);
                float z = float.Parse(c);
                tuple_array[0] = new tuple_3 { x = x, y = y, z = z };
                faces.Add(tuple_array);
            }
        }

        //Guarda los valores de las texturas.
        private void save_vt(string a, string b, string c, int index)
        {
            float x = float.Parse(a);
            float y = float.Parse(b);
            float z = float.Parse(c);
            vt_dic.Add(index, new tuple_3 { x = x, y = y, z = z });
        }

        //Guarda los valores de las normales.
        private void save_vn(string a, string b, string c, int index)
        {
            float x = float.Parse(a);
            float y = float.Parse(b);
            float z = float.Parse(c);
            vn_dic.Add(index, new tuple_3 { x = x, y = y, z = z });
        }

        //Para cada triangulo, computa su normal y luego guarda la info de la normal de los vertices.
        private void compute_vertex_normals()
        {
            foreach (tuple_3[] face in faces)
            {
                int v1_index = (int)face[0].x;
                int v2_index = (int)face[0].y;
                int v3_index = (int)face[0].z;

                tuple_3 v1 = vertex[v1_index];
                tuple_3 v2 = vertex[v2_index];
                tuple_3 v3 = vertex[v3_index];

                //Saco la normal de la cara, y la sumo a la normal asignada a cada vector en su diccionario.
                tuple_3 normal = Vectores.Normalize(Vectores.cross((v2 - v1), (v3 - v1)));

                if (!vertex_normals.ContainsKey(v1_index))
                    vertex_normals.Add(v1_index, normal);
                else if (vertex_normals.ContainsKey(v1_index))
                    vertex_normals[v1_index] = vertex_normals[v1_index] + normal;

                if (!vertex_normals.ContainsKey(v2_index))
                    vertex_normals.Add(v2_index, normal);
                else if (vertex_normals.ContainsKey(v2_index))
                    vertex_normals[v2_index] = vertex_normals[v2_index] + normal;

                if (!vertex_normals.ContainsKey(v3_index))
                    vertex_normals.Add(v3_index, normal);
                else if (vertex_normals.ContainsKey(v3_index))
                    vertex_normals[v3_index] = vertex_normals[v3_index] + normal;
            }

            vn_array = new Vector3[vertex_normals.Count + 1];

            for (int i = 1; i <= vertex_normals.Count; i++)
            {
                tuple_3 vec = Vectores.Normalize(vertex_normals[i]);
                vertex_normals[i] = vec;
                vn_array[i - 1] = new Vector3(vec.x, vec.y, vec.z);
            }
        }
    }
}
