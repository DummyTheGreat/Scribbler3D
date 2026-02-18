shader_type spatial;


uniform vec4 glow_color : source_color = vec4(0.2, 0.8, 1.0, 1.0);
uniform float glow_strength = 4.0;
uniform float rim_power = 2.0;

void fragment() {
    // View direction in view space and normal
    float ndotv = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
    float rim = pow(1.0 - ndotv, rim_power);

    ALBEDO = vec3(1.0); // unshaded; you can also sample texture if desired
    EMISSION = glow_color.rgb * rim * glow_strength;
    ALPHA = 1.0;
}