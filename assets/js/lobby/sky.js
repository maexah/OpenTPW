import{THREE as n,rng as R}from"./kit.js";export const MOODS={"lost-kingdom":{high:5288685,low:6606823,cloud:16182502,cloudShade:12568269,cover:.42,haze:7327974,sun:16773842,sunPower:1,air:16777215,ground:11126690,airPower:1},"wonder-land":{high:2535671,low:2278905,cloud:16777215,cloudShade:14086399,cover:.1,haze:5230583,sun:16777215,sunPower:1.05,air:16777215,ground:11852710,airPower:1.05},"halloween-world":{high:2587317,low:2269877,cloud:1530475,cloudShade:1000272,cover:.86,haze:2792880,sun:11131370,sunPower:.4,air:11128797,ground:5206392,airPower:.82},"space-zone":{high:3968982,low:15043186,cloud:6829126,cloudShade:4857904,cover:.3,haze:7107752,sun:16762016,sunPower:.8,air:14470143,ground:8415642,airPower:.92}};const m=["high","low","cloud","cloudShade","haze","sun","air","ground"],p=["cover","sunPower","airPower"],b=new Map(Object.entries(MOODS).map(([r,a])=>{const t={};for(const e of m)t[e]=new n.Color(a[e]);for(const e of p)t[e]=a[e];return[r,t]}));export function emptyMood(){const r={};for(const a of m)r[a]=new n.Color;for(const a of p)r[a]=0;return r}export function mixMoods(r,a){for(const t of m)a[t].setRGB(0,0,0);for(const t of p)a[t]=0;for(const{id:t,share:e}of r){const f=b.get(t)??b.get("lost-kingdom");for(const o of m)a[o].r+=f[o].r*e,a[o].g+=f[o].g*e,a[o].b+=f[o].b*e;for(const o of p)a[o]+=f[o]*e}return a}function L(r,a){const t=R(a),e=new Uint8Array(r*r*4),f=[[4,.5],[8,.27],[16,.15],[32,.08]];for(let u=0;u<3;u++){const k=f.map(([l])=>Float32Array.from({length:l*l},()=>t()));for(let l=0;l<r;l++)for(let s=0;s<r;s++){let g=0;f.forEach(([i,M],P)=>{const h=k[P],w=s/r*i,y=l/r*i,x=Math.floor(w),v=Math.floor(y),C=(x+1)%i,S=(v+1)%i;let c=w-x,d=y-v;c=c*c*(3-2*c),d=d*d*(3-2*d);const T=h[v*i+x]*(1-c)+h[v*i+C]*c,D=h[S*i+x]*(1-c)+h[S*i+C]*c;g+=(T*(1-d)+D*d)*M}),e[(l*r+s)*4+u]=Math.round(g*255)}}for(let u=3;u<e.length;u+=4)e[u]=255;const o=new n.DataTexture(e,r,r,n.RGBAFormat);return o.wrapS=o.wrapT=n.RepeatWrapping,o.magFilter=n.LinearFilter,o.minFilter=n.LinearMipmapLinearFilter,o.generateMipmaps=!0,o.needsUpdate=!0,o}const F=`
	varying vec3 vDir;

	void main()
	{
		vDir = position;
		gl_Position = projectionMatrix * viewMatrix * vec4( position + cameraPosition, 1.0 );
	}
`,N=`
	uniform vec3 uHigh;
	uniform vec3 uLow;
	uniform vec3 uCloud;
	uniform vec3 uCloudShade;
	uniform float uCover;
	uniform float uReach;
	uniform float uTime;
	uniform sampler2D uNoise;
	varying vec3 vDir;

	void main()
	{
		vec3 dir = normalize( vDir );
		float up = max( dir.y, 0.0 );
		vec3 colour = mix( uLow, uHigh, smoothstep( 0.0, uReach, up ) );

		// Three cloud layers on a flat ceiling far overhead, each sliding at its own speed.
		vec2 uv = dir.xz / ( up + 0.2 );
		float a = texture2D( uNoise, uv * 0.07 + uTime * vec2( 0.0028, 0.0006 ) ).r;
		float b = texture2D( uNoise, uv * 0.15 + uTime * vec2( 0.0050, -0.0012 ) ).g;
		float c = texture2D( uNoise, uv * 0.33 + uTime * vec2( 0.0084, 0.0021 ) ).b;
		float n = a * 0.55 + b * 0.3 + c * 0.15;

		float start = mix( 0.71, 0.31, uCover );
		float cloud = smoothstep( start, start + 0.15, n ) * smoothstep( 0.0, 0.16, dir.y ) * 0.9;
		float thick = smoothstep( start + 0.08, start + 0.3, n );

		colour = mix( colour, mix( uCloud, uCloudShade, thick * 0.75 ), cloud );

		gl_FragColor = vec4( colour, 1.0 );
		#include <colorspace_fragment>
	}
`;export function createSky({reach:r=.5}={}){const a=L(128,77),t=new n.ShaderMaterial({uniforms:{uHigh:{value:new n.Color},uLow:{value:new n.Color},uCloud:{value:new n.Color},uCloudShade:{value:new n.Color},uCover:{value:.4},uReach:{value:r},uTime:{value:0},uNoise:{value:a}},vertexShader:F,fragmentShader:N,side:n.BackSide,depthTest:!1,depthWrite:!1}),e=new n.Mesh(new n.SphereGeometry(900,24,12),t);return e.frustumCulled=!1,e.renderOrder=-10,{mesh:e,update(f,o){const u=t.uniforms;u.uTime.value=f,u.uHigh.value.copy(o.high),u.uLow.value.copy(o.low),u.uCloud.value.copy(o.cloud),u.uCloudShade.value.copy(o.cloudShade),u.uCover.value=o.cover},dispose(){e.geometry.dispose(),t.dispose(),a.dispose()}}}
