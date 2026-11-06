shader_type canvas_item;

// The flat map, drawn only where the install has no quad cache to draw the original's surface from
// (docs/mechanics/mapdraw.md, section 6) - a mod with a map of its own and no cache. Nothing of it is drawn over the surface.
//
// Draws the map from which province every point belongs to and what colour each
// province is - two colours each, the second striped over the first where they differ.
// Recolouring the world - a province changing hands, another map mode - is then a write
// to two small textures and nothing else.

// One texel per map pixel, north at the top. The province id is red + 256 * green.
uniform sampler2D province_ids : filter_nearest, repeat_disable;
// One texel per province, 256 to a row: texel (id % 256, id / 256).
uniform sampler2D palette : filter_nearest, repeat_disable;
// A second colour for each province, laid out the same way. Where it differs from the
// first the province is striped with it: ground one country holds, the first colour,
// and another owns, the second and the thinner.
uniform sampler2D stripe_palette : filter_nearest, repeat_disable;
// The original's own stripes, whose alpha says how much of the second colour a point
// takes. It is laid over the map once every STRIPE_TILE map pixels, north at its top.
uniform sampler2D stripes : filter_linear_mipmap, repeat_enable;
uniform bool has_stripes = false;
uniform ivec2 map_size = ivec2(1, 1);

const float STRIPE_TILE = 8.0;

// 0 hides the lines between provinces of one colour, 1 shows them in full. The view
// raises it with the zoom, because from far away they are noise.
uniform float province_border = 0.0;
uniform int hovered_province = 0;
// A texel a province, lit where the province is selected.
uniform sampler2D selection : filter_nearest, repeat_disable;
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

vec3 stripe_colour_of(int province) {
	return texelFetch(stripe_palette, ivec2(province % 256, province / 256), 0).rgb;
}

// How strongly the edge shared with a neighbouring texel should be drawn: 2 where the
// first colour changes across it, 1 where only the province does, 0 where nothing does.
// The stripes do not make a border: the heavy line runs where the holder changes, so
// ground a country has taken is inside its own.
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
	vec3 here_colour = colour_of(here);
	vec3 here_stripe = stripe_colour_of(here);
	vec3 colour = here_colour;
	if (has_stripes && here_stripe != here_colour) {
		// The original measures its stripes from the map's southern edge, so the
		// texture is turned over here, where y runs south.
		vec2 at = (vec2(map_position.x, float(map_size.y) - map_position.y) + 0.5) / STRIPE_TILE;
		colour = mix(here_colour, here_stripe, texture(stripes, at).a);
	}

	if (here != 0 && texelFetch(selection, ivec2(here % 256, here / 256), 0).r > 0.5) {
		colour = mix(colour, vec3(1.0), 0.45);
	} else if (here != 0 && here == hovered_province) {
		colour = mix(colour, vec3(1.0), 0.2);
	}

	// Map pixels per screen pixel. A line is a fixed width on screen, so its width in
	// map pixels shrinks as the view zooms in.
	float scale = max(fwidth(map_position.x), fwidth(map_position.y));
	float country_width = min(COUNTRY_LINE * scale, 0.5);
	float province_width = min(PROVINCE_LINE * scale, 0.5);

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
