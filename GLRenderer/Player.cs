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
    class Player : Object
    {
        //non-static variables:
        public static float MAXSPEED = 20;
        public static float MAXTURNSPEED = 120;
        public static float speed = 0;
        public static float turnSpeed = 0;
        public float jump_velocity = 5f;
        public float jump_time = 0.0f;
        public float min_z;

        public bool jumping { get; set; }
        public bool moving { get; set; }
        public tuple_3 face_vector { get; set; }
        public int switch_frames = 15;

        public tuple_3 fp_camPos { get; set; }
        public tuple_3 front_pos { get; set; }
        public tuple_3 back_pos { get; set; }
        public tuple_3 right_pos { get; set; }
        public tuple_3 left_pos { get; set; }

        //Constructor que recibe los parametros tipicos, ademas de otros que pueda querer usar:
        public Player(Vector3[] vertexPositionData, Vector3[] vertexNormalData, Vector3[] vertexTextureData, uint[] facesData, float[] transformationMatrix, float[] materialColor) 
        : base(vertexPositionData, vertexNormalData, vertexTextureData, facesData, transformationMatrix, materialColor)
        {   }
    }
}
