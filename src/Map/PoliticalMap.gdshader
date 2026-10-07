shader_type canvas_item;

// Draws the map from two textures: which province every point belongs to, and what
// colour each province is. Recolouring the world - a province changing hands, another
// map mode - is then a write to the small texture and nothing else.

// One texel per map pixel, north at the top. The province id is red + 256 * green.
uniform sampler2D province_ids : filter_nearest, repeat_disable;
// One texel per province, 256 to a row: texel (id % 256, id / 256).
uniform sampler2D palette : filter_nearest, repeat_disable;
uniform ivec2 map_size = ivec2(1, 1);

// 0 hides the lines between provinces of one colour, 1 shows them in full. The view
// raises it with the zoom, because from far away they are noise.
uniform float province_border = 0.0;
uniform int hovered_province = 0;
uniform int selected_province = 0;

// Line half-widths, in screen pixels.
const float COUNTRY_LINE = 0.9;
const float PROVINCE_LINE = 0.5;

varying vec2 map_position;

void vertex() {
	// The rectangle is drawn in map pixels, so the vertex position is the map position.
	map_position = VERTEX;
}

int province_at(ivec2 texel) {
	// x wraps, because the map is a cylinder. y does not. The rectangle is three maps
	// wide starting one map to the west, so adding two widths keeps the operand of %
	// positive, which it has to be: the remainder of a negative int is undefined.
	texel.x = (texel.x + 2 * map_size.x) % map_size.x;
	texel.y = clamp(texel.y, 0, map_size.y - 1);
	vec2 encoded = texelFetch(province_ids, texel, 0).rg;
	return int(encoded.r * 255.0 + 0.5) + int(encoded.g * 255.0 + 0.5) * 256;
}

vec3 colour_of(int province) {
	return texelFetch(palette, ivec2(province % 256, province / 256), 0).rgb;
}

// How strongly the edge shared with a neighbouring texel should be drawn: 2 where the
// colour changes across it, 1 where only the province does, 0 where nothing does.
float edge_kind(int here, vec3 here_colour, ivec2 neighbour) {
	int there = province_at(neighbour);
	if (there == here) {
		return 0.0;
	}
	return colour_of(there) != here_colour ? 2.0 : 1.0;
}

void fragment() {
	ivec2 texel = ivec2(floor(map_position));
	vec2 within = fract(map_position);
	int here = province_at(texel);
	vec3 colour = colour_of(here);

	if (here != 0 && here == selected_province) {
		colour = mix(colour, vec3(1.0), 0.45);
	} else if (here != 0 && here == hovered_province) {
		colour = mix(colour, vec3(1.0), 0.2);
	}

	// Map pixels per screen pixel. A line is a fixed width on screen, so its width in
	// map pixels shrinks as the view zooms in.
	float scale = max(fwidth(map_position.x), fwidth(map_position.y));
	float country_width = min(COUNTRY_LINE * scale, 0.5);
	float province_width = min(PROVINCE_LINE * scale, 0.5);

	vec3 here_colour = colour_of(here);
	float left = edge_kind(here, here_colour, texel + ivec2(-1, 0));
	float right = edge_kind(here, here_colour, texel + ivec2(1, 0));
	float up = edge_kind(here, here_colour, texel + ivec2(0, -1));
	float down = edge_kind(here, here_colour, texel + ivec2(0, 1));

	float country = 0.0;
	float province = 0.0;
	if (left > 0.0 && within.x < (left > 1.5 ? country_width : province_width)) {
		if (left > 1.5) { country = 1.0; } else { province = 1.0; }
	}
	if (right > 0.0 && within.x > 1.0 - (right > 1.5 ? country_width : province_width)) {
		if (right > 1.5) { country = 1.0; } else { province = 1.0; }
	}
	if (up > 0.0 && within.y < (up > 1.5 ? country_width : province_width)) {
		if (up > 1.5) { country = 1.0; } else { province = 1.0; }
	}
	if (down > 0.0 && within.y > 1.0 - (down > 1.5 ? country_width : province_width)) {
		if (down > 1.5) { country = 1.0; } else { province = 1.0; }
	}

	colour = mix(colour, colour * 0.72, province * province_border * (1.0 - country));
	colour = mix(colour, colour * 0.25, country);

	COLOR = vec4(colour, 1.0);
}
