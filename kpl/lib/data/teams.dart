import 'package:flutter/material.dart';

/// Static franchise information. Crests are drawn procedurally from these
/// colours (no official logos are used).
class TeamInfo {
  const TeamInfo({
    required this.id,
    required this.name,
    required this.city,
    required this.venue,
    required this.venueShort,
    required this.primary,
    required this.secondary,
    required this.glow,
    required this.titleYears,
    required this.group,
    required this.batFactor,
    required this.spinFactor,
    required this.boundary,
    required this.finish2026,
    required this.captain2026,
  });

  final String id;
  final String name;
  final String city;
  final String venue;
  final String venueShort;
  final Color primary;
  final Color secondary;

  /// Bright accent used for glows and highlights on dark backgrounds.
  final Color glow;
  final List<int> titleYears;
  final String group;

  /// Venue character: >1 favours batting, spinFactor >1 helps spinners.
  final double batFactor;
  final double spinFactor;

  /// Average boundary distance in metres.
  final int boundary;
  final int finish2026;
  final String captain2026;

  int get titles => titleYears.length;
}

const kTeams = <TeamInfo>[
  TeamInfo(
    id: 'CSK',
    name: 'Chennai Super Kings',
    city: 'Chennai',
    venue: 'M. A. Chidambaram Stadium',
    venueShort: 'Chepauk',
    primary: Color(0xFFFDB913),
    secondary: Color(0xFF0B4CA3),
    glow: Color(0xFFFFD43B),
    titleYears: [2010, 2011, 2018, 2021, 2023],
    group: 'A',
    batFactor: 0.95,
    spinFactor: 1.14,
    boundary: 66,
    finish2026: 8,
    captain2026: 'Ruturaj Gaikwad',
  ),
  TeamInfo(
    id: 'MI',
    name: 'Mumbai Indians',
    city: 'Mumbai',
    venue: 'Wankhede Stadium',
    venueShort: 'Wankhede',
    primary: Color(0xFF0B5BC4),
    secondary: Color(0xFFD9B347),
    glow: Color(0xFF4DA3FF),
    titleYears: [2013, 2015, 2017, 2019, 2020],
    group: 'B',
    batFactor: 1.07,
    spinFactor: 0.95,
    boundary: 64,
    finish2026: 9,
    captain2026: 'Hardik Pandya',
  ),
  TeamInfo(
    id: 'RCB',
    name: 'Royal Challengers Bengaluru',
    city: 'Bengaluru',
    venue: 'M. Chinnaswamy Stadium',
    venueShort: 'Chinnaswamy',
    primary: Color(0xFFD71920),
    secondary: Color(0xFF1C1C1C),
    glow: Color(0xFFFF3B3B),
    titleYears: [2025, 2026],
    group: 'A',
    batFactor: 1.13,
    spinFactor: 0.92,
    boundary: 62,
    finish2026: 1,
    captain2026: 'Rajat Patidar',
  ),
  TeamInfo(
    id: 'KKR',
    name: 'Kolkata Knight Riders',
    city: 'Kolkata',
    venue: 'Eden Gardens',
    venueShort: 'Eden Gardens',
    primary: Color(0xFF3F2470),
    secondary: Color(0xFFF2C14E),
    glow: Color(0xFFA77BFF),
    titleYears: [2012, 2014, 2024],
    group: 'A',
    batFactor: 1.05,
    spinFactor: 1.04,
    boundary: 65,
    finish2026: 7,
    captain2026: 'Ajinkya Rahane',
  ),
  TeamInfo(
    id: 'SRH',
    name: 'Sunrisers Hyderabad',
    city: 'Hyderabad',
    venue: 'Rajiv Gandhi Intl. Stadium',
    venueShort: 'Uppal',
    primary: Color(0xFFF26522),
    secondary: Color(0xFF161616),
    glow: Color(0xFFFF8A3D),
    titleYears: [2016],
    group: 'B',
    batFactor: 1.10,
    spinFactor: 0.97,
    boundary: 66,
    finish2026: 3,
    captain2026: 'Pat Cummins',
  ),
  TeamInfo(
    id: 'RR',
    name: 'Rajasthan Royals',
    city: 'Jaipur',
    venue: 'Sawai Mansingh Stadium',
    venueShort: 'SMS Jaipur',
    primary: Color(0xFFE91E8C),
    secondary: Color(0xFF1E3D8F),
    glow: Color(0xFFFF4FAE),
    titleYears: [2008],
    group: 'A',
    batFactor: 0.98,
    spinFactor: 1.04,
    boundary: 68,
    finish2026: 4,
    captain2026: 'Riyan Parag',
  ),
  TeamInfo(
    id: 'DC',
    name: 'Delhi Capitals',
    city: 'Delhi',
    venue: 'Arun Jaitley Stadium',
    venueShort: 'Kotla',
    primary: Color(0xFF17479E),
    secondary: Color(0xFFEF1C25),
    glow: Color(0xFF4F86FF),
    titleYears: [],
    group: 'B',
    batFactor: 1.08,
    spinFactor: 1.0,
    boundary: 63,
    finish2026: 6,
    captain2026: 'Axar Patel',
  ),
  TeamInfo(
    id: 'PBKS',
    name: 'Punjab Kings',
    city: 'Mullanpur',
    venue: 'Maharaja Yadavindra Singh Stadium',
    venueShort: 'Mullanpur',
    primary: Color(0xFFDD1F2D),
    secondary: Color(0xFFC9CCD1),
    glow: Color(0xFFFF5A64),
    titleYears: [],
    group: 'A',
    batFactor: 1.03,
    spinFactor: 0.96,
    boundary: 67,
    finish2026: 5,
    captain2026: 'Shreyas Iyer',
  ),
  TeamInfo(
    id: 'GT',
    name: 'Gujarat Titans',
    city: 'Ahmedabad',
    venue: 'Narendra Modi Stadium',
    venueShort: 'Motera',
    primary: Color(0xFF1B2A4A),
    secondary: Color(0xFFC9A64B),
    glow: Color(0xFFE2C26A),
    titleYears: [2022],
    group: 'B',
    batFactor: 1.02,
    spinFactor: 1.0,
    boundary: 70,
    finish2026: 2,
    captain2026: 'Shubman Gill',
  ),
  TeamInfo(
    id: 'LSG',
    name: 'Lucknow Super Giants',
    city: 'Lucknow',
    venue: 'Ekana Cricket Stadium',
    venueShort: 'Ekana',
    primary: Color(0xFF1D5BD8),
    secondary: Color(0xFFFF7A1A),
    glow: Color(0xFF4FB4FF),
    titleYears: [],
    group: 'B',
    batFactor: 0.94,
    spinFactor: 1.06,
    boundary: 68,
    finish2026: 10,
    captain2026: 'Rishabh Pant',
  ),
];

final Map<String, TeamInfo> kTeamById = {for (final t in kTeams) t.id: t};

TeamInfo teamById(String id) => kTeamById[id]!;

/// Country chip colours (no flag images needed).
const kCountryColors = <String, Color>{
  'IND': Color(0xFF1E88E5),
  'AUS': Color(0xFFFFC107),
  'ENG': Color(0xFF1565C0),
  'SA': Color(0xFF2E7D32),
  'NZ': Color(0xFF263238),
  'WI': Color(0xFF8D1B3D),
  'AFG': Color(0xFF1B5E20),
  'SL': Color(0xFF283593),
  'ZIM': Color(0xFFC62828),
  'BAN': Color(0xFF00695C),
};

const kCountryNames = <String, String>{
  'IND': 'India',
  'AUS': 'Australia',
  'ENG': 'England',
  'SA': 'South Africa',
  'NZ': 'New Zealand',
  'WI': 'West Indies',
  'AFG': 'Afghanistan',
  'SL': 'Sri Lanka',
  'ZIM': 'Zimbabwe',
  'BAN': 'Bangladesh',
};
