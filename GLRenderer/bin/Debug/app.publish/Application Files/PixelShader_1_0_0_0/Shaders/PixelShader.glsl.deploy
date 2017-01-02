#version 130
precision highp float;

#define LCOUNT 10

// Recibe: Normal, Position (world space) y coordUV (outputs de vertex shader).
// Estos valores ya fueron ponderados baricentricamente por openGl.
in vec3 outNormal;
in vec3 outPosition;
in vec3 outUV;

//Color de este pixel.
out vec4 pixelColor;

//Uniforms. Por ahora asumo maximo 10 luces por escena. Maximo dos colores de material:
uniform vec3[LCOUNT] lightPositionArray;
uniform vec3[LCOUNT] lightColorArray;
uniform vec3[2] materialColorArray;
uniform vec3 cameraPosition;
uniform int lightCount;
uniform sampler2D textureSampler;
uniform int shininess;
uniform vec3 ambientColor;


//Este metodo se encargara de obtener el color final de shading:---------------------------------------------------------
vec3 material_management()
{
	vec3 vision_dir = normalize(cameraPosition - outPosition);
	bool using_lambert = true;
	bool using_blinnphong = true;

	//Usare -1 en primera componente para distinguir nulls de no nulls:
	vec3 obj_difuse_color = vec3(-1.0f, 0.0f, 0.0f);
	vec3 obj_specular_color = vec3(-1.0f, 0.0f, 0.0f);
	vec3 obj_ambient_color = vec3(-1.0f, 0.0f, 0.0f);
	obj_difuse_color = materialColorArray[0];
	obj_specular_color = materialColorArray[1];

	//POR AHORA NO USARE AMBIENTE
	if (ambientColor != vec3(0,0,0))
    {
    	if (obj_difuse_color.r != -1.0f) //&& use_for_ambient)
			obj_ambient_color = ambientColor * obj_difuse_color;
    }
	vec3 difuse_color_sum = vec3(0.0f,0.0f,0.0f);
	vec3 specular_color_sum = vec3(0.0f,0.0f,0.0f);

	//Aqui se encarga de las luces y los brillos difusos y especular:
    for (int i = 0; i < lightCount; i++)
    {
        vec3 light_dir = normalize(lightPositionArray[i] - outPosition);
		vec3 light_color = lightColorArray[i]; 

		if (using_lambert)
		{
			float cos_theta = dot(outNormal, light_dir);
			float f_dif = max(0.0f, cos_theta);
			vec3 difuse_color = vec3(f_dif * light_color.x, f_dif * light_color.y, f_dif * light_color.z);
			difuse_color_sum += difuse_color;
		}
		if (using_blinnphong)
		{
			vec3 h = normalize((light_dir + vision_dir) / 2.0f);
			float cos_theta2 = dot(outNormal, h);
			float f_spec = pow(max(0.0f, cos_theta2), shininess);
			vec3 specular_color = vec3(f_spec * light_color.x, f_spec * light_color.y, f_spec * light_color.z);
			specular_color_sum += specular_color;
		}        
    }

	if (using_lambert)
		obj_difuse_color *= difuse_color_sum;
	if (using_blinnphong)
		obj_specular_color *= specular_color_sum;
	
    //Color final que tendra el punto en cuestion:
    vec3 obj_color = vec3(0.0f, 0.0f, 0.0f);     
    if (using_lambert)
        obj_color += obj_difuse_color;
    if (using_blinnphong)
        obj_color += obj_specular_color;
    if (obj_ambient_color.x != -1.0f )//&& scene.scene_use_ambient)
    	obj_color += obj_ambient_color;
   
    return obj_color;
}


void main(void)
{ 
	//Usamos el sampler y coordenadas UV para obtener color de textura:
	vec4 texColor = texture(textureSampler, outUV.xy);

	vec4 shadeColor = vec4(material_management(), 1);
	
	pixelColor = vec4(shadeColor + vec4(texColor.rgb, 1));
}