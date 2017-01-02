#version 130
      
precision highp float;

//Por ahora me da lo mismo el color de los vertices, no se incluiran.

in vec3 inPosition;
in vec3 inNormal;
in vec3 inUV;

out vec3 outNormal;
out vec3 outPosition;
out vec3 outUV;

uniform mat4x4 transformationMatrix;

uniform mat4x4 rotationMatrix;
uniform mat4x4 translationMatrix;
uniform mat4x4 scaleMatrix;
uniform mat4x4 T1Matrix;
uniform mat4x4 T2Matrix;

void main(void)
{
	
	//mat4x4 objectMatrix = translationMatrix * T1Matrix * rotationMatrix * T2Matrix * scaleMatrix;
	mat4x4 objectMatrix = translationMatrix * rotationMatrix * scaleMatrix;
	
	gl_Position = transformationMatrix * objectMatrix  * vec4(inPosition, 1);
	outNormal = (rotationMatrix * vec4(inNormal, 1)).xyz;
	outPosition = (objectMatrix * vec4(inPosition, 1)).xyz;
	outUV = inUV;
}