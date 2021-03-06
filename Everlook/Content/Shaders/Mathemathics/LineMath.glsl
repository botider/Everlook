//
//  LineMath.glsl
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

#ifndef LineMath_I
#define LineMath_I

/// <summary>
/// Calculates the distance from a point to a line in screen space.
/// This function has some issues when the distance is taken up close, and it becomes
/// inaccurate.
/// </summary>
/// <param name="F">The origin point.</param>
/// <param name="Q">A point on the line.</param>
/// <param name="QDir">The direction vector of the line.</param>
/// <returns>The distance from F to Q.</returns>
float DistanceToLine(vec2 F, vec2 Q, vec2 QDir)
{
	vec2 nearestPointOnLine = Q + QDir * dot(F - Q, QDir);
	return distance(F, nearestPointOnLine);
}

#endif
