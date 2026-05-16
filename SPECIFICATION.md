# Project Specification: Morkosia PrepaLeague

## 1. Introduction

This document outlines the requirements for the Morkosia PrepaLeague application for El Morkosia church. The application supports multiple fantasy league formats, starting with:
*   **Activity Points League:** Managers build teams within a budget, using players who may be on multiple teams (up to a cap), with optional manually enabled transfer windows. Points are primarily based on member participation in defined activities.
*   **Head-to-Head (H2H) League:** Participants are assigned roles (Manager or Member-only) within their league, teams have exclusive player ownership, rosters are locked for the season, and managers compete in direct matchups.

The goal is to create a web-based system to increase student engagement in Sunday school activities through fun, competitive fantasy leagues.

## 2. Goals

*   Increase student engagement in Sunday school activities.
*   Provide a fun, competitive element based on football fantasy structure.
*   Allow students to track their fantasy team's performance.
*   Enable the teacher (Admin) to manage activities, points, team structure, and team management windows.

## 3. User Roles

*   **Student:** A registered Sunday school student who can self-register and log in to participate in leagues as assigned.
*   **Admin:** A user (likely a teacher or assistant) granted privileges by a Super Admin to manage specific assigned leagues. They can manage league settings, participants (within their assigned leagues), activities, and scoring for those leagues.
*   **Super Admin:** A top-level administrator (likely the main teacher) with full control over the entire system. They can manage all leagues, all users, assign Admins to leagues, and manage system-wide settings.

## 4. Functional Requirements

### 4.1 Core Concepts

*   **Member:** A Sunday school student who participates in activities and can be selected for a fantasy team. Each Member has an assigned **Price**.
*   **Manager:** A Sunday school student who selects and manages a fantasy team within a given **Budget**.
*   **Position:** A role within the fantasy team. The defined positions and required counts per team are:
    *   1 Goalkeeper (GK)
    *   2 Defenders (DEF)
    *   2 Midfielders (MID)
    *   1 Attacker (ATT)
*   **Team:** A selection of Members chosen by a Manager, with each Member assigned to a specific Position slot. The team size and position structure is configured by the Admin when creating the league, with a typical structure being 1 GK, 2 DEF, 2 MID, 1 ATT (6 total). The total Price of the selected Members must not exceed the Manager's Budget. **Each Member can only be selected once per team (cannot occupy multiple positions on the same team).**
*   **Team History:** The system maintains a complete historical record of all team selections across transfer windows for points calculation purposes. When points are calculated, the system uses the team composition that was active during the specific period when the activities occurred, not the current team selection.
*   **Price:** The cost assigned to a Member, chosen from predefined tiers (5M, 10M, 15M, 20M, 25M). Set by the Admin.
*   **Budget:** The total amount (90M) a Manager has to spend on the Prices of their 6 team Members.
*   **Activity Type:** A category of rewardable action (e.g., "Attend Mass Prayer", "Attending Tasbe7a") with default points assigned. Activity Types are linked to one or more Positions by the Admin.
*   **Concrete Activity:** A specific instance of an Activity Type occurring on a particular date or occasion (e.g., "Attend Mass Prayer - 2025-05-04") with points that default to the Activity Type's points but can be overridden.
*   **Score:** Points accumulated by a Manager's team. Scores are calculated dynamically based on the Concrete Activities completed by their selected Members, *only if* the Activity Type is relevant to the Position the Member is assigned to *on that specific Manager's team*.
*   **Transfer Window:** A specific period activated on-demand by the Admin (with a defined duration, e.g., 7 hours) during which Managers can select their teams. **Only one transfer window can be active per league at any given time.** When a Transfer Window opens, previously selected teams are cleared. **The system maintains a complete historical record of all team selections from previous transfer windows for points calculation purposes.** **All transfer window times (start, end, duration) must be specified and stored in UTC.** Activities are attributed to a transfer window based on their date: an activity belongs to Transfer Window N if its date falls on or after the window's start date and before the next window's start date (if a next window exists).

### 4.2 Student (Manager) Features (MVP)

*   **REQ-S01:** Users must be able to self-register for an account by providing a username, password, full name, and class. Once registered, they can log in to the web application using their credentials.
*   **REQ-S02:** When a Transfer Window opens, the Manager's previous team selection is cleared.
*   **REQ-S03:** During an active Transfer Window, Managers must select a new team of exactly 6 Members, assigning each Member to a valid Position slot (1 GK, 2 DEF, 2 MID, 1 ATT).
*   **REQ-S04:** Team selection must adhere to the following rules:
    *   **Rule 1:** The team must consist of exactly 1 GK, 2 DEF, 2 MID, and 1 ATT.
    *   **Rule 2:** The total Price of the 6 selected Members must not exceed the Manager's Budget (90M).
    *   **Rule 3:** Each Member can only be selected once per team (a Member cannot occupy multiple positions on the same team).
    *   *(System Enforced Rule): A specific Member cannot be selected for a Position if they are already selected for that Position on the maximum number of teams allowed by their individual Member Position-Based Ownership Cap configuration for that Member-Position combination.* Managers should ideally be informed during the selection process if a player is unavailable for a specific position due to this cap.
*   **REQ-S05:** Managers must be able to view their current team selection (Members, their assigned Positions, and their Prices) at any time.
*   **REQ-S06:** Managers must be able to view the overall leaderboard. The leaderboard should display dynamically calculated scores for all Managers, ranked highest to lowest.
*   **REQ-S07:** Managers should see their own team's total score, calculated dynamically.
*   **REQ-S08:** Outside of an active Transfer Window, Managers cannot change their team selection or Position assignments.

### 4.3 Admin Features (MVP)

*These features apply to Admins operating within the leagues they are assigned to manage.*

*   **REQ-A01:** View and manage student participation in their assigned leagues.
*   **REQ-A02:** (Activity Points League Specific) Assign Price to each Member.
*   **REQ-A03:** Create and configure leagues with specific settings:
    *   Team size (between 3 and 20 members)
    *   Position structure (e.g., 1 GK, 2 DEF, 2 MID, 1 ATT for a 6-member team)
    *   League type (ActivityPoints or H2H)
    *   (Activity Points League Specific) Member Position-Based Ownership Caps for each member-position combination
*   **REQ-A04:** (Activity Points League Specific) Link Activity Types to Positions.
*   **REQ-A04B:** (Activity Points League Specific) Configure Member Position-Based Ownership Caps (maximum number of teams each specific Member can be selected for each specific Position).
*   **REQ-A04C:** (Activity Points League Specific) View current Member-Position selection counts to help make informed decisions about ownership cap adjustments.
*   **REQ-A04D:** (Activity Points League Specific) View historical team selections across transfer windows for auditing and points calculation verification purposes.
*   **REQ-A05:** Define Activity Types (potentially global or per-league).
*   **REQ-A06:** Create Concrete Activities.
*   **REQ-A07:** Record Member participation in Concrete Activities.
*   **REQ-A08:** (Activity Points League Specific) Open/Close Transfer Window for their assigned Activity Points league(s). **Only one transfer window can be active per league at any given time.**
*   **REQ-A09:** (H2H League Specific) Create/Manage H2H leagues they are assigned to.
*   **REQ-A10:** (H2H League Specific) Assign participating Students to their H2H league(s).
*   **REQ-A11:** (H2H League Specific) Set the fixed Team Size for their H2H league(s).
*   **REQ-A12:** (H2H League Specific) Manually assign Members to Manager teams in their H2H league(s).
*   **REQ-A13:** (H2H League Specific) Define Scoring Periods for their H2H league(s).
*   **REQ-A14:** (H2H League Specific) Manually define Matchups for their H2H league(s).
*   **REQ-A15:** (H2H League Specific) Manage the Season State of their H2H league(s).

### 4.4 Super Admin Features (MVP)

*   **REQ-SA01:** Create, manage (e.g., reset password, deactivate), and delete Admin accounts.
*   **REQ-SA02:** Assign specific Admins the permission to manage one or more specific league instances (Classic or H2H).
*   **REQ-SA03:** Have full access and control over all leagues, settings, users, and activities, overriding Admin permissions if necessary.
*   **REQ-SA04:** Define global settings if applicable (e.g., default Activity Types, Position structures if not per-league).

### 5. System Features (MVP)

*   **REQ-SYS01:** The system must dynamically calculate the total points for a Manager's team upon request. The calculation sums points from Concrete Activities completed by the Members on that Manager's team, **but only if the Activity Type of the Concrete Activity is linked (by the Admin) to the Position that the Member is assigned to on that specific Manager's team.** **Points are calculated based on the team composition that was active during the period when the activities occurred, using historical team data.**
*   **REQ-SYS02:** The system must rank Managers on the leaderboard based on their dynamically calculated total scores.
*   **REQ-SYS03:** The system must enforce the team selection rules during the Transfer Window:
    *   Positional structure (1 GK, 2 DEF, 2 MID, 1 ATT).
    *   Budget constraint (Total Price <= 90M).
    *   Single selection per team (Each Member can only be selected once per team, cannot occupy multiple positions on the same team).
    *   Member Position-Based Ownership Cap (Prevent selection of a Member for a Position if they are already selected for that Position on the maximum number of teams allowed by their individual cap for that Member-Position combination).
*   **REQ-SYS04:** The system must prevent team/position changes outside the active Transfer Window.
*   **REQ-SYS05:** The system must automatically recognize the Transfer Window as closed once its specified duration has passed. **Transfer window times must be handled in UTC.**
*   **REQ-SYS06:** The system must clear Manager team selections when a new Transfer Window is opened by the Admin. **Only one transfer window can be active per league at any given time.**
*   **REQ-SYS07:** The system must track and maintain counts of how many times each Member is selected for each Position across all teams in each Activity Points league to enforce Member Position-Based Ownership Caps.
*   **REQ-SYS08:** The system must maintain complete historical records of all team selections across transfer windows to enable accurate points calculation based on the team composition that was active during specific activity periods.
*   **REQ-SYS09:** The system must perform team selection and ownership cap validations within an atomic transaction using optimistic concurrency control, ensuring availability checks and team persistence happen as one indivisible operation to prevent race conditions.

### 6. System Features (MVP) - Permissions

*   **REQ-SYS-P01:** The system must enforce permissions based on user roles (Student, Admin, Super Admin).
*   **REQ-SYS-P02:** Admins must only be able to perform management actions (as defined in Admin Features) on the specific league instances they have been assigned by a Super Admin.
*   **REQ-SYS-P03:** Super Admins must have unrestricted access to all system functions and data.

## 7. Activity Points League Requirements

*(This section contains the requirements for the budget-based, activity-scored league)*

### 7.1 Activity Points League Concepts

*   **Position:** Role within the fantasy team (configurable structure, typically 1 GK, 2 DEF, 2 MID, 1 ATT).
*   **Team (Activity Points):** Selection of Members assigned to Positions, respecting the configured structure.
*   **Price:** Cost assigned to a Member (5M, 10M, 15M, 20M, 25M). Target distribution: 8/16/16/12/8.
*   **Budget:** 90M limit for team Price.
*   **Member Position-Based Ownership Cap:** A configurable limit that specifies the maximum number of times each specific Member can be selected for each specific Position across all teams in the league. This is configured per Member per Position by the Admin (e.g., Member A: GK max 1 team, DEF max 2 teams; Member B: GK max 3 teams, MID max 1 team). If not configured for a specific member-position combination, a default cap applies.
*   **Position-Activity Link:** Activity Types are linked to Positions by the Admin. A Member only earns points for a Manager if the Activity Type matches the Member's assigned Position on that Manager's team.
*   **Transfer Window:** Admin-activated period (with duration) where Managers can change teams. **Only one transfer window can be active per league at any given time.** Teams are cleared when a window opens. **All transfer window times (start, end, duration) must be specified and stored in UTC.** Activities are attributed to a transfer window based on their date: an activity belongs to Transfer Window N if its date falls on or after the window's start date and before the next window's start date (if a next window exists).

#### 7.1.1 Member Position-Based Ownership Cap Details

The Member Position-Based Ownership Cap system works as follows:

*   **Per-Member Per-Position Limits:** Each specific Member can have individual maximum ownership limits configured for each Position they can play (e.g., Member A: GK max 1, DEF max 2; Member B: GK max 3, MID max 1).
*   **Member-Position-Specific Counting:** The system tracks how many teams each specific Member is currently selected for each specific Position.
*   **Example Configuration:** 
    ```json
    "memberPositionCaps": {
        "MemberA_UserId": { "GK": 1, "DEF": 2 },
        "MemberB_UserId": { "GK": 3, "MID": 1, "ATT": 2 },
        "MemberC_UserId": { "DEF": 5 }
    }
    ```
*   **Example Scenario:** If Member A is selected as GK on 1 team and DEF on 1 team, then Member A cannot be selected as GK on any more teams (reached limit of 1), but can still be selected as DEF on 1 more team (limit is 2).
*   **Selection Blocking:** A Member cannot be selected for a Position if they are already selected for that Position on the maximum number of teams allowed for that specific Member-Position combination.
*   **Default Configuration:** If no specific cap is configured for a Member-Position combination, a league-wide default cap applies to that Member-Position combination.
*   **Admin Flexibility:** This allows Admins to precisely control which members are more or less available for specific positions (e.g., limiting a star goalkeeper to only 1 team while allowing them to play defense on multiple teams).

#### 7.1.2 Member Position-Based Ownership Cap Configuration

*   **Configuration Format:** The configuration is stored as a nested structure where each Member's UserId maps to a dictionary of Position names and their maximum counts.
*   **Partial Configuration:** Admins only need to configure caps for Member-Position combinations they want to limit. Unconfigured combinations use the league default.
*   **Position Names:** Must match the exact Position names defined in the league's team structure (e.g., "GK", "DEF", "MID", "ATT").
*   **Zero Cap:** Setting a cap of 0 for a Member-Position combination effectively prevents that Member from being selected for that Position.
*   **No Cap:** If a Member-Position combination is not configured and no league default exists, the system should allow unlimited selections (or use a system-wide maximum as a safety measure).

### 7.2 Activity Points League - Manager Features (MVP)

*   **REQ-AP-S01:** Log in.
*   **REQ-AP-S02:** Team cleared when Transfer Window opens.
*   **REQ-AP-S03:** Select/modify 6-Member team (1 GK, 2 DEF, 2 MID, 1 ATT) during Transfer Window.
*   **REQ-AP-S04:** Team selection adheres to:
    *   Positional structure.
    *   Budget constraint (<= 90M).
    *   Single selection per team (Each Member can only be selected once per team, cannot occupy multiple positions on the same team).
    *   Member Position-Based Ownership Cap (Member available for a Position if they are selected for that Position on fewer teams than their configured maximum for that Member-Position combination).
*   **REQ-AP-S05:** View current team (Members, Positions, Prices).
*   **REQ-AP-S06:** View overall Activity Points leaderboard (dynamically calculated scores, ranked).
*   **REQ-AP-S07:** View own total score (dynamically calculated).
*   **REQ-AP-S08:** Cannot change team outside Transfer Window.
*   **REQ-AP-S09:** Receive clear feedback when a Member cannot be selected for a Position due to their Member Position-Based Ownership Cap being reached (e.g., "Player A cannot be selected as GK - already on maximum 1 team for this position").

### 7.3 Activity Points League - Admin Features (MVP)

// ... (REQ-A01 remains generic) ...
*   **REQ-A02:** (Activity Points League Specific) Assign Price to each Member.
*   **REQ-A03:** (Activity Points League Specific) Define Position structure (if configurable per league, otherwise Super Admin).
*   **REQ-A04:** (Activity Points League Specific) Link Activity Types to Positions.
*   **REQ-A04B:** (Activity Points League Specific) Configure Member Position-Based Ownership Caps (maximum number of teams each specific Member can be selected for each specific Position).
*   **REQ-A04C:** (Activity Points League Specific) View current Member-Position selection counts to help make informed decisions about ownership cap adjustments.
*   **REQ-A04D:** (Activity Points League Specific) View historical team selections across transfer windows for auditing and points calculation verification purposes.
// ... (REQ-A05 to REQ-A07 remain generic) ...
*   **REQ-A08:** (Activity Points League Specific) Open/Close Transfer Window for their assigned Activity Points league(s). **Only one transfer window can be active per league at any given time.**
// ... (REQ-A09 onwards relate to H2H or are generic) ...

### 7.4 Activity Points League - System Features (MVP)

*   **REQ-AP-SYS01:** Dynamically calculate Manager score based on Member points *if* Activity Type matches assigned Position. **Points are calculated using the team composition that was active during the period when the activities occurred, based on historical team data.**
*   **REQ-AP-SYS02:** Rank Managers on leaderboard.
*   **REQ-AP-SYS03:** Enforce selection rules (Position, Budget, Single selection per team, Member Position-Based Ownership Cap).
*   **REQ-AP-SYS04:** Prevent changes outside Transfer Window.
*   **REQ-AP-SYS05:** Auto-close Transfer Window.
*   **REQ-AP-SYS06:** Clear teams when Transfer Window opens.
*   **REQ-AP-SYS07:** Track Member-Position selection counts across all teams to enforce Member Position-Based Ownership Caps during team selection.
*   **REQ-AP-SYS08:** Maintain complete historical records of all team selections across transfer windows to enable accurate points calculation based on the team composition that was active during specific activity periods.

## 8. Head-to-Head (H2H) League Requirements

### 8.1 H2H League Concepts

*   **H2H League:** A distinct league instance with a fixed set of participating students assigned by the Admin.
*   **Manager (H2H):** A student designated by the Admin to have a team within a specific H2H league.
*   **Exclusive Ownership:** Within a specific H2H League instance, each Member can only be assigned to **one** Manager's team. A student designated as a Manager cannot also be assigned as a Member to another team *within the same H2H league*.
*   **Team Size (H2H):** Fixed per H2H league instance, set by Admin during league creation (between 3 and 20 members, with recommended sizes being 6, 7, or 8 Members).
*   **Team (H2H):** A fixed roster of Members manually assigned by the Admin to a Manager designated for that H2H league. Assignment respects the league's Team Size and Exclusive Ownership rules. The roster is locked for the duration of the H2H league season.

### 8.2 H2H League - Manager Features (MVP)

*   **REQ-H2H-S01:** Log in (shared).
*   **REQ-H2H-S02:** View their assigned H2H team roster.
*   **REQ-H2H-S03:** View the H2H league schedule (past and upcoming Matchups defined by Admin).
*   **REQ-H2H-S04:** View their specific Matchup results (Win/Loss/Tie and scores).
*   **REQ-H2H-S05:** View the H2H league standings (W/L/T records, total points).

### 8.3 H2H League - Admin Features (MVP)

*   **REQ-H2H-A01:** Create a new H2H League instance.
*   **REQ-H2H-A02:** Assign participating Students to the H2H league.
*   **REQ-H2H-A03:** Set the fixed Team Size (6, 7, or 8) for the H2H League.
*   **REQ-H2H-A04:** Manually assign Members (students participating in the league who are not designated Managers for this league) to each designated Manager's team, ensuring exclusive ownership and correct team size.
*   **REQ-H2H-A05:** Define the Scoring Periods for the H2H league season (e.g., Week 1 dates, Week 2 dates, etc.).
*   **REQ-H2H-A06:** Manually define specific Matchups for the H2H league (e.g., Manager A vs. Manager B for Scoring Period 'Week 1').
*   **REQ-H2H-A07:** Manage the Season State of an H2H league (e.g., Start Season, Pause Season, End Season).
*   *(Admin also uses shared features: REQ-CL-A01, REQ-CL-A05, REQ-CL-A06, REQ-CL-A07)*

### 8.4 H2H League - System Features (MVP)

*   **REQ-H2H-SYS01:** Calculate the total score for an H2H team for a given Scoring Period by summing the points from all Concrete Activities completed by all Members on that team during that period.
*   **REQ-H2H-SYS02:** Determine the Matchup Result (Win/Loss/Tie) based on the calculated scores of the two opposing teams for the relevant Scoring Period (using the Matchups defined by the Admin).
*   **REQ-H2H-SYS03:** Calculate and display H2H league standings based on W/L/T records and total points.
*   **REQ-H2H-SYS04:** Ensure Admin adheres to exclusive ownership and team size rules during manual team assignment (REQ-H2H-A04). **Additionally, ensure that each Member can only be selected once per team (cannot occupy multiple positions on the same team).**
*   **REQ-H2H-SYS05:** Prevent any changes to H2H team rosters once the season is Active.
*   **REQ-H2H-SYS06:** Maintain historical records of H2H team compositions to enable accurate points calculation based on the team roster that was active during specific scoring periods.

## 9. General User Features (MVP)

*   **REQ-GEN-S01:** Students must be able to log in.
*   **REQ-GEN-S02:** Students must have a dashboard or overview page showing the leagues they are participating in.
*   **REQ-GEN-S03:** From the dashboard, students must be able to navigate to view details specific to each league they are in (e.g., view Activity Points team/leaderboard, view H2H team/schedule/standings).

## 10. Cross-League Considerations (MVP)

*   **REQ-XL-01:** A Student account can participate in multiple leagues simultaneously (e.g., one Activity Points, one H2H, or multiple of the same type if supported by Admin setup).
*   **REQ-XL-02:** A Member (student available for selection) is part of a global pool. Their participation in activities (Concrete Activities) generates points relevant for scoring calculations in whichever league(s) they are selected in (respecting Classic position rules and H2H exclusivity rules *within each specific league instance*).

## 11. Non-Functional Requirements

*   **NFR-01:** The web interface should be simple and intuitive for students, including navigating between multiple leagues if applicable.
*   **NFR-02:** System configuration and league management should be manageable by the Admin (initially via DB/simple forms, later via richer UI).
*   **NFR-03:** The system architecture should be designed with extensibility in mind, allowing for the potential addition of new league types or variations in the future (e.g., using base classes/interfaces for league concepts).

## 12. Future Considerations (Post-MVP)

*   **Activity Points:** Captain points, more complex scoring, Admin web interfaces.
*   **H2H:** Automated draft system, automated schedule generation, playoffs, consolation brackets, waivers/free agency (relaxing team lock), customizable H2H settings.
*   **General:** Notifications, historical data, point breakdown views, richer Admin controls, more sophisticated multi-league dashboard/navigation.
*   **Team History:** Enhanced historical analytics, team performance trending over time, detailed transfer window analysis, and historical comparison tools for managers to analyze their team selection patterns across seasons.
