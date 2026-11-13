using System;
using UnityEngine;

/// <summary>Maps confirmed scenarios and escalation levels to explicit intervention rules.</summary>
public class InterventionPolicyResolver
{
    public Intervention Resolve(
    UserScenarioType userScenarioType,
    int escalationCount,
    PlayerSpatialState spatialState,
    PlayerTurnStatus turnStatus,
    TaskContextType taskContext)
    {
        return userScenarioType switch
        {
            UserScenarioType.NotOptimalPositionForTooLong =>
                Resolve_NotOptimalPositionForTooLong(escalationCount, spatialState, turnStatus, taskContext),

            UserScenarioType.DidNotMoveInTime =>
                Resolve_DidNotMoveInTime(escalationCount),

            UserScenarioType.Missed_2_Or_3_PortalsThisRound or
            UserScenarioType.Missed_2_Or_3_Portals_For_2_Round =>
                ResolveGazeIntervention(userScenarioType, escalationCount),

            _ => throw new ArgumentOutOfRangeException($"No policy defined for {userScenarioType}")
        };
    }


    private Intervention Resolve_DidNotMoveInTime(int count)
    {
        return count switch
        {
            0 => new Intervention
            {
                Nature = InterventionNature.Verbal,
                VerbalInterventionType = VerbalInterventionType.RepeatInstruction,
                Description = "Please make a selection."
            },
            1 => new Intervention
            {
                Nature = InterventionNature.Verbal,
                VerbalInterventionType = VerbalInterventionType.RepeatUsingName,
                Description = "Hey [UserName], please choose one."
            },
            2 => new Intervention
            {
                Nature = InterventionNature.Visual,
                VisualSubCategory = VisualSubCategory.SystemGenerated,
                VisualInterventionType = VisualInterventionType.DrawPath,
                SystemPathTarget = SystemPathTarget.PlayZone,  // Not needed here, but populated for formatting 
                Description = "Drawing a path to the selection area."
            },
            3 => new Intervention
            {
                Nature = InterventionNature.Visual,
                VisualSubCategory = VisualSubCategory.AvatarBehaviour,
                VisualInterventionType = VisualInterventionType.DemoAction,
                VisualTargetType = VisualTargetType.Avatar_HelpTo_MakeSelection,
                Description = "Avatar demonstrates how to make the selection."
            },

            _ => new Intervention
            {
                Nature = InterventionNature.Verbal,
                VerbalInterventionType = VerbalInterventionType.PauseTalking,
                Description = "Fallback intervention."
            }
        };
    }


    private Intervention Resolve_NotOptimalPositionForTooLong(
    int count,
    PlayerSpatialState spatialState,
    PlayerTurnStatus turnStatus,
    TaskContextType context)
    {
        switch (context)
        {
            case TaskContextType.ListeningToInstruction:
                return ResolveListeningContext(count, spatialState);

            case TaskContextType.WaitingForSelection:
                return ResolveSelectionContext(count, spatialState);

            case TaskContextType.ReturningToBase:
                return ResolveReturningContext(count, spatialState);

            case TaskContextType.FreeExploration:
                return ResolveExplorationContext(count, spatialState);

            default:
                Debug.LogWarning($"[Policy Resolver] Unhandled context type: {context}");
                return null; // Or handle this in the caller gracefully.
        }
    }



    private Intervention ResolveListeningContext(int count, PlayerSpatialState spatialState)
    {
        switch (count)
        {
            case 0:
                if (spatialState == PlayerSpatialState.InZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePortals,
                        Description = "Please look toward the portals."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_FacingPortals ||
                         spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZone,
                        Description = "Please return to your player zone to make a selection."
                    };
                }
                break;

            case 1:
                if (spatialState == PlayerSpatialState.InZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePortalsWithName,
                        Description = "[Name], please look toward the portals so we can continue."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_FacingPortals ||
                         spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZoneWithName,
                        Description = "[Name], please come back to your player zone to get started."
                    };
                }
                break;

            case 2:
                if (spatialState == PlayerSpatialState.NotInZone_FacingPortals ||
                          spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Visual,
                        VisualSubCategory = VisualSubCategory.SystemGenerated,
                        VisualInterventionType = VisualInterventionType.DrawPath,
                        SystemPathTarget = SystemPathTarget.PlayerZone,
                        Description = "Drawing path to guide user back to player zone."
                    };
                }
                break;

            case 3:
                return new Intervention
                {
                    Nature = InterventionNature.Visual,
                    VisualSubCategory = VisualSubCategory.AvatarBehaviour,
                    VisualInterventionType = VisualInterventionType.DemoAction,
                    VisualTargetType = VisualTargetType.Avatar_HelpTo_FacePortals,
                    Description = "Avatar shows users where to turn to face portals."
                };
        }

        return new Intervention
        {
            Nature = InterventionNature.ReTraining,
            ReTrainingInterventionType = ReTrainingInterventionType.MovementTraining,
            Description = "Starting movement retraining."
        };
    }

    private Intervention ResolveSelectionContext(int count, PlayerSpatialState spatialState)
    {
        switch (count)
        {
            case 0:
                if (spatialState == PlayerSpatialState.InZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePortals,
                        Description = "Please face the portals to make a choice."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_FacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.First_Generic_Positive_Preferred,
                        Description = "User is out of zone in the right direction, stuck at making selection."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZoneToRestart,
                        Description = "User is out of zone in the wrong direction, return to zone to restart"
                    };
                }
                break;

            case 1:
                if (spatialState == PlayerSpatialState.InZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePortalsWithName,
                        Description = "[Name], please face the portals to make a choice."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_FacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.Second_Generic_Positive_WithName,
                        Description = "[Name], you're close! Just make your choice when you're ready."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZoneToRestart,
                        Description = "[Name], return to your zone to start again."
                    };
                }
                break;

            case 2:
                if (spatialState == PlayerSpatialState.InZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Visual,
                        VisualSubCategory = VisualSubCategory.SystemGenerated,
                        VisualInterventionType = VisualInterventionType.DrawPath,
                        SystemPathTarget = SystemPathTarget.PlayZone,
                        Description = "Draw Path to play zone to engorge the player to make selection"
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_FacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Visual,
                        VisualSubCategory = VisualSubCategory.SystemGenerated,
                        VisualInterventionType = VisualInterventionType.DrawPath,
                        SystemPathTarget = SystemPathTarget.PlayZone,
                        Description = "Draw path to play zone"
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Visual,
                        VisualSubCategory = VisualSubCategory.SystemGenerated,
                        VisualInterventionType = VisualInterventionType.DrawPath,
                        SystemPathTarget = SystemPathTarget.PlayerZone,
                        Description = "Draw path to player zone to restart"
                    };
                }
                break;

            case 3:
                return new Intervention
                {
                    Nature = InterventionNature.Visual,
                    VisualSubCategory = VisualSubCategory.AvatarBehaviour,
                    VisualInterventionType = VisualInterventionType.DemoAction,
                    VisualTargetType = VisualTargetType.Avatar_HelpTo_MakeSelection,
                    Description = "Avatar demonstrates walking to and into a portal."
                };
        }

        return new Intervention
        {
            Nature = InterventionNature.ReTraining,
            ReTrainingInterventionType = ReTrainingInterventionType.MovementTraining,
            Description = "Starting movement retraining."
        };
    }

    private Intervention ResolveReturningContext(int count, PlayerSpatialState spatialState)
    {
        switch (count)
        {
            case 0:
                if (spatialState == PlayerSpatialState.NotInZone_FacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePlayerZone,
                        Description = "Please face player zone to start returning"
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZone,
                        Description = "Please return to your player zone."
                    };
                }
                break;

            case 1:
                if (spatialState == PlayerSpatialState.NotInZone_FacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePlayerZoneWithName,
                        Description = "[Name], please turn around and head back to your zone."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZoneWithName,
                        Description = "[Name], please return to your player zone now."
                    };
                }
                break;

            case 2:
                return new Intervention
                {
                    Nature = InterventionNature.Visual,
                    VisualSubCategory = VisualSubCategory.SystemGenerated,
                    VisualInterventionType = VisualInterventionType.DrawPath,
                    SystemPathTarget = SystemPathTarget.PlayerZone,
                    Description = "Draw path to return to player zone for guidance."
                };

            case 3:
                return new Intervention
                {
                    Nature = InterventionNature.Visual,
                    VisualSubCategory = VisualSubCategory.AvatarBehaviour,
                    VisualInterventionType = VisualInterventionType.DemoAction,
                    VisualTargetType = VisualTargetType.Avatar_HelpTo_ReturnToPlayerZone,
                    Description = "Avatar shows user how walks back to player zone."
                };
        }

        return new Intervention
        {
            Nature = InterventionNature.ReTraining,
            ReTrainingInterventionType = ReTrainingInterventionType.MovementTraining,
            Description = "Starting movement retraining."
        };
    }

    private Intervention ResolveExplorationContext(int count, PlayerSpatialState spatialState)
    {
        switch (count)
        {
            case 0:
                if (spatialState == PlayerSpatialState.InZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePortals,
                        Description = "Please face the portals to continue."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_FacingPortals ||
                         spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZone,
                        Description = "Please face the portals to continue."
                    };
                }
                break;

            case 1:
                if (spatialState == PlayerSpatialState.InZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.FacePortalsWithName,
                        Description = "[Name], please look toward the portals."
                    };
                }
                else if (spatialState == PlayerSpatialState.NotInZone_FacingPortals ||
                         spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Verbal,
                        VerbalInterventionType = VerbalInterventionType.ReturnToZoneWithName,
                        Description = "[Name], please return to your zone to continue."
                    };
                }
                break;

            case 2:
                if (spatialState == PlayerSpatialState.NotInZone_FacingPortals ||
                                 spatialState == PlayerSpatialState.NotInZone_NotFacingPortals)
                {
                    return new Intervention
                    {
                        Nature = InterventionNature.Visual,
                        VisualSubCategory = VisualSubCategory.AvatarBehaviour,
                        VisualInterventionType = VisualInterventionType.DrawPath,
                        SystemPathTarget = SystemPathTarget.PlayerZone,
                        Description = "Draw a path to  get user to return to their zone"
                    };
                }
                break;


            case 3:
                return new Intervention
                {
                    Nature = InterventionNature.Visual,
                    VisualSubCategory = VisualSubCategory.AvatarBehaviour,
                    VisualInterventionType = VisualInterventionType.DemoAction,
                    VisualTargetType = VisualTargetType.Avatar_HelpTo_ReturnToPlayerZone,
                    Description = "Avatar orients toward portals and gestures."
                };
        }

        return new Intervention
        {
            Nature = InterventionNature.ReTraining,
            ReTrainingInterventionType = ReTrainingInterventionType.MovementTraining,
            Description = "Starting movement retraining."
        };
    }

    private Intervention ResolveGazeIntervention(UserScenarioType userScenarioType, int count)
    {
        switch (userScenarioType)
        {
            case UserScenarioType.Missed_2_Or_3_PortalsThisRound:
                return ResolveSimpleMissedPortals();

            case UserScenarioType.Missed_2_Or_3_Portals_For_2_Round:
                return ResolveFullGazeRetraining();

            default:
                Debug.LogWarning($"[Resolver] Unhandled Gaze Intervention Scenario: {userScenarioType}");
                return null;
        }
    }


    private Intervention ResolveSimpleMissedPortals()
    {
        return new Intervention
        {
            Nature = InterventionNature.Verbal,
            VerbalInterventionType = VerbalInterventionType.RemindScanAllPortals,  // you can adjust your enum here
            Description = "Please make sure to scan all portals carefully before selecting."
        };
    }

    private Intervention ResolveFullGazeRetraining()
    {
        return new Intervention
        {
            Nature = InterventionNature.ReTraining,
            ReTrainingInterventionType = ReTrainingInterventionType.GazeTraining,
            Description = "Starting gaze retraining procedure."
        };
    }
}
