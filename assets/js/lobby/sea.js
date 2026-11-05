import{THREE as e}from"./kit.js";const t=`
	varying vec3 vWorld;

	void main()
	{
		vec4 world = modelMatrix * vec4( position, 1.0 );
		vWorld = world.xyz;
		gl_Position = projectionMatrix * viewMatrix * world;
	}
`,i=`
	uniform float uTime;
	uniform float uReach;
	uniform vec3 uEye;
	uniform vec3 uNear;
	uniform vec3 uBand;
	uniform vec3 uFar;
	uniform vec3 uFarther;
	uniform vec3 uEdge;
	varying vec3 vWorld;

	void main()
	{
		float d = distance( vWorld.xz, uEye.xz ) / uReach;

		// Fine ripples close by: wavy bands, bent by two slow waves so they never look ruled. They slide one way
		// and sway. Further off they give way to a broader swell, and then to plain colour, so nothing shimmers.
		vec2 p = vWorld.xz;
		float bend = sin( p.x * 0.31 + uTime * 0.2 ) * 1.3 + sin( p.x * 0.13 - p.y * 0.21 + uTime * 0.13 ) * 1.7;
		float fine = smoothstep( 0.05, 0.75, sin( p.y * 2.2 + bend + uTime * 0.5 ) );
		float swell = sin( p.x * 0.1 + uTime * 0.09 ) * 1.4 + sin( p.y * 0.05 + p.x * 0.07 - uTime * 0.06 ) * 1.6;
		float broad = smoothstep( 0.15, 0.9, sin( p.y * 0.62 + swell + uTime * 0.22 ) );

		float band = fine * 0.85 * ( 1.0 - smoothstep( 55.0, 150.0, d ) );
		band = max( band, broad * 0.6 * smoothstep( 40.0, 110.0, d ) * ( 1.0 - smoothstep( 170.0, 400.0, d ) ) );

		vec3 colour = mix( uNear, uBand, band );
		colour = mix( colour, uFar, smoothstep( 50.0, 250.0, d ) );
		colour = mix( colour, uFarther, smoothstep( 230.0, 700.0, d ) );
		colour = mix( colour, uEdge, smoothstep( 650.0, 2000.0, d ) );

		gl_FragColor = vec4( colour, 1.0 );
		#include <colorspace_fragment>
	}
`;export function createSea(){const o=new e.ShaderMaterial({uniforms:{uTime:{value:0},uReach:{value:1},uEye:{value:new e.Vector3},uNear:{value:new e.Color(415080)},uBand:{value:new e.Color(408649)},uFar:{value:new e.Color(1806508)},uFarther:{value:new e.Color(3853038)},uEdge:{value:new e.Color(4513279)}},vertexShader:t,fragmentShader:i}),r=new e.Mesh(new e.CircleGeometry(2600,48).rotateX(-Math.PI/2),o);return r.frustumCulled=!1,{mesh:r,update(u,a,n){r.position.set(a.x,0,a.z),o.uniforms.uTime.value=u,o.uniforms.uReach.value=n,o.uniforms.uEye.value.copy(a)},dispose(){r.geometry.dispose(),o.dispose()}}}
