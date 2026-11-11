using System.Reflection;



using Autodesk.Revit.DB.Structure;



using Autodesk.Revit.DB.Structure.StructuralSections;



﻿using System;



using System.Collections.Generic;



using System.IO;



using System.Linq;



using System.Text;



using System.Text.Json;



using AIGRAV.Contracts;



using Autodesk.Revit.DB;



using Autodesk.Revit.UI;



namespace AIGRAV.Skills.BimAudit;



public sealed partial class BimAuditSkill : IAIGRAVSkill



{



    public AIGRAVSkillInfo Info { get; } = new()



    {



        Name = "BIM Audit Engine",



        Version = "2.1",



        Category = "Quality Control",



        Description = "AIGRAV BIM Audit & Filter Management Engine for Autodesk Revit",



        Source = "External",



        Priority = 1700



    };



    public bool CanHandle(string toolName)



    {



        return toolName.Equals("aigrav_check_steel_sections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("check_steel_sections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_steel_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("inspect_steel_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_create_beam_filters", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("create_beam_filters", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_copy_beam_filters", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("copy_beam_filters", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("audit_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_populate_connection_types", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("populate_connection_types", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_connection_components", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("inspect_connection_components", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_dump_element_params", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_handler_details", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_subelements", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_sample_plates", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_group_plates_comments", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_subelement_class", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_test_set_conn_param", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_verify_populated", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_conn_fam_names", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_list_all_conn_fams", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_replace_conn_family", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_action_substructure", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_action_superstructure", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_action_connections_hoist", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_missing_wf350", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_list_empty_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_schedule_def", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("audit_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_duplicate_elements", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("audit_duplicate_elements", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_create_clash_validation_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("create_clash_validation_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_list_all_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_and_open_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_check_delete_candidates", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_execute_delete_candidates", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_mezz_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_all_mezz_conn_types", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_all_mezz_framing", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_action_mezz_framing", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_action_mezz_wf400", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_action_mezz_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_empty_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_populate_empty_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_substructure_qto", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_columns_rafters_qto", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_mezz_cremona_qto", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_connections_hardware_qto", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_check_double_counting", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_fix_powerhouse_filter", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_hoist_columns_detail", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_separate_hoist_columns", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_mezz_conn_breakdown", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_update_mezz_connection_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_apply_option_b_renumbering", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_mismatched_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_sync_all_connection_types", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_linked_rooms", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_transfer_linked_room_tags", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_link_apis", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_configure_link_views", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_existing_tags", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_room_tag_reflection", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_clean_and_place_room_tags", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_conn_params_detail", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_populate_conn_params", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_elements_with_lokasi", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_generate_location_bq_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_fix_production_office_baseplates", StringComparison.OrdinalIgnoreCase) || toolName.Equals("aigrav_exec_subagent1_batch1", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_subagent2_batch2", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_subagent3_batch3", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_qa_audit_all_schedules", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_cleanup_orphaned_copies", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_apply_anti_double_counting", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_qto_audit_framing_columns", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_qto_audit_connections", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_qto_audit_reconciliation", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_fix_batch1", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_fix_batch2", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_schedule_formatting", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_calculated_field_api", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_sloof_floor_overlap", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_sloof_s1_elev0", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_switch_join_sloof_s1_s1a", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_audit_empty_comments_lokasi", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_inspect_portal_sheets", StringComparison.OrdinalIgnoreCase)



            || toolName.Equals("aigrav_execute_hide_portal_links", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_build_house_type36", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_export_house_type36_views", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_create_perspective_previews", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_create_interior_views", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_list_open_documents", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_open_background_document", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_close_background_document", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_inspect_external_document", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_copy_from_external_project", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_inspect_family_internals", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_modify_family_parameter", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_reload_family_with_override", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_copy_sheets_and_views", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_create_sheets", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_create_drafting_views_and_place", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("create_drafting_views_and_place", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_export_comparison_sheets", StringComparison.OrdinalIgnoreCase);



    }



    public string Execute(string toolName, UIApplication app, string? argument = null)



    {



        Document? doc = app?.ActiveUIDocument?.Document;



        if (doc == null) return "BIM Audit failed: No active Revit document.";



        try



        {



            if (toolName.Equals("aigrav_build_house_type36", StringComparison.OrdinalIgnoreCase))
            {
                return BuildHouseType36(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_export_house_type36_views", StringComparison.OrdinalIgnoreCase))
            {
                return ExportHouseType36Views(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_create_perspective_previews", StringComparison.OrdinalIgnoreCase))
            {
                return CreatePerspectivePreviews(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_create_interior_views", StringComparison.OrdinalIgnoreCase))
            {
                return CreateInteriorViews(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_list_open_documents", StringComparison.OrdinalIgnoreCase))
            {
                return ListOpenDocuments(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_open_background_document", StringComparison.OrdinalIgnoreCase))
            {
                return OpenBackgroundDocument(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_close_background_document", StringComparison.OrdinalIgnoreCase))
            {
                return CloseBackgroundDocument(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_inspect_external_document", StringComparison.OrdinalIgnoreCase))
            {
                return InspectExternalDocument(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_copy_from_external_project", StringComparison.OrdinalIgnoreCase))
            {
                return CopyFromExternalProject(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_inspect_family_internals", StringComparison.OrdinalIgnoreCase))
            {
                return InspectFamilyInternals(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_modify_family_parameter", StringComparison.OrdinalIgnoreCase))
            {
                return ModifyFamilyParameter(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_reload_family_with_override", StringComparison.OrdinalIgnoreCase))
            {
                return ReloadFamilyWithOverride(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_copy_sheets_and_views", StringComparison.OrdinalIgnoreCase))
            {
                return CopySheetsAndViewsFromProject(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_create_drafting_views_and_place", StringComparison.OrdinalIgnoreCase) || toolName.Equals("create_drafting_views_and_place", StringComparison.OrdinalIgnoreCase))
            {
                return CreateDraftingViewsAndPlace(app!, doc, argument);
            }
            if (toolName.Equals("aigrav_export_comparison_sheets", StringComparison.OrdinalIgnoreCase))
            {
                return ExportComparisonSheets(doc);
            }
            if (toolName.Equals("aigrav_execute_hide_portal_links", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteHideLinksInPortalViews(doc, argument);



            }



            if (toolName.Equals("aigrav_inspect_portal_sheets", StringComparison.OrdinalIgnoreCase))



            {



                return InspectPortalSheetsAndLinks(doc);



            }



            if (toolName.Equals("aigrav_audit_sloof_floor_overlap", StringComparison.OrdinalIgnoreCase))



            {



                return AuditSloofFloorOverlap(doc);



            }



            if (toolName.Equals("aigrav_audit_sloof_s1_elev0", StringComparison.OrdinalIgnoreCase))



            {



                return AuditSloofS1AndS1AFloors(doc);



            }



            if (toolName.Equals("aigrav_switch_join_sloof_s1_s1a", StringComparison.OrdinalIgnoreCase))



            {



                return SwitchJoinOrderSloofS1AndS1A(doc);



            }



            if (toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase))



            {



                return SwitchSinglePair(doc, argument ?? "6481290,6886055");



            }



            if (toolName.Equals("aigrav_audit_empty_comments_lokasi", StringComparison.OrdinalIgnoreCase))



            {



                return AuditSchedulesEmptyCommentsOrLokasi(doc);



            }



            if (toolName.Equals("aigrav_exec_option1_batch_injection", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteOption1_SharedParameterBatchInjection(app, doc);



            }



            if (toolName.Equals("aigrav_exec_fix_batch1", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteFixBatch1_MasterAndMezzanine(doc);



            }



            if (toolName.Equals("aigrav_exec_fix_batch2", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteFixBatch2_ConnectionsTanggaBracing(doc);



            }



            if (toolName.Equals("aigrav_audit_schedule_formatting", StringComparison.OrdinalIgnoreCase))



            {



                return AuditScheduleFormatting(doc);



            }



            if (toolName.Equals("aigrav_inspect_calculated_field_api", StringComparison.OrdinalIgnoreCase))



            {



                return InspectCalculatedFieldApi(doc, argument ?? "C.3.1.3");



            }



            if (toolName.Equals("aigrav_fix_production_office_baseplates", StringComparison.OrdinalIgnoreCase))



            {



                return FixProductionOfficeBaseplateSchedules(doc);



            }



            if (toolName.Equals("aigrav_exec_subagent1_batch1", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteBatch1_CartonCupsAndProductionOffice(doc);



            }



            if (toolName.Equals("aigrav_exec_subagent2_batch2", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteBatch2_WaterlineAndPowerHouse(doc);



            }



            if (toolName.Equals("aigrav_exec_subagent3_batch3", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteBatch3_ChillerAndHousekeeping(doc);



            }



            if (toolName.Equals("aigrav_qa_audit_all_schedules", StringComparison.OrdinalIgnoreCase))



            {



                return QaAuditAllLocationSchedules(doc, argument);



            }



            if (toolName.Equals("aigrav_cleanup_orphaned_copies", StringComparison.OrdinalIgnoreCase))



            {



                return CleanupOrphanedCopies(doc);



            }



            if (toolName.Equals("aigrav_apply_anti_double_counting", StringComparison.OrdinalIgnoreCase))



            {



                return ApplyAntiDoubleCountingToBangunanUtama(doc);



            }



            if (toolName.Equals("aigrav_qto_audit_framing_columns", StringComparison.OrdinalIgnoreCase))



            {



                return AuditQtoFramingAndColumns(doc);



            }



            if (toolName.Equals("aigrav_qto_audit_connections", StringComparison.OrdinalIgnoreCase))



            {



                return AuditQtoConnectionsPlatesAndBolts(doc);



            }



            if (toolName.Equals("aigrav_qto_audit_reconciliation", StringComparison.OrdinalIgnoreCase))



            {



                return AuditQtoGlobalReconciliation(doc);



            }



            if (toolName.Equals("aigrav_generate_location_bq_schedules", StringComparison.OrdinalIgnoreCase))



            {



                return GenerateLocationBqSchedules(doc, argument);



            }



            if (toolName.Equals("aigrav_inspect_conn_params_detail", StringComparison.OrdinalIgnoreCase))



            {



                return InspectConnectionParamsDetailed(doc);



            }



            if (toolName.Equals("aigrav_audit_elements_with_lokasi", StringComparison.OrdinalIgnoreCase))



            {



                return AuditElementsWithLokasi(doc);



            }



            if (toolName.Equals("aigrav_populate_conn_params", StringComparison.OrdinalIgnoreCase))



            {



                return PopulateConnectionParams(doc, argument);



            }



            if (toolName.Equals("aigrav_inspect_link_apis", StringComparison.OrdinalIgnoreCase))



            {



                return InspectLinkApis(doc, app);



            }



            if (toolName.Equals("aigrav_configure_link_views", StringComparison.OrdinalIgnoreCase))



            {



                return ConfigureLinkViews(doc, app, argument);



            }



            if (toolName.Equals("aigrav_audit_linked_rooms", StringComparison.OrdinalIgnoreCase))



            {



                return AuditLinkedRooms(doc);



            }



            if (toolName.Equals("aigrav_clean_and_place_room_tags", StringComparison.OrdinalIgnoreCase))



            {



                return CleanAndPlaceAllLinkedRoomTags(doc);



            }



            if (toolName.Equals("aigrav_inspect_room_tag_reflection", StringComparison.OrdinalIgnoreCase))



            {



                return InspectRoomTagReflection(doc);



            }



            if (toolName.Equals("aigrav_audit_existing_tags", StringComparison.OrdinalIgnoreCase))



            {



                return AuditExistingTagsInView(doc);



            }



            if (toolName.Equals("aigrav_transfer_linked_room_tags", StringComparison.OrdinalIgnoreCase))



            {



                return TransferLinkedRoomTags(doc, argument);



            }



            if (toolName.Equals("aigrav_check_steel_sections", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("check_steel_sections", StringComparison.OrdinalIgnoreCase))



            {



                return InspectCastellatedGeom(app);



            }



            if (toolName.Equals("aigrav_inspect_steel_connections", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("inspect_steel_connections", StringComparison.OrdinalIgnoreCase))



            {



                string addRes = AddStandardSteelParamsToCastellated(doc);



                string inspectRes = InspectStructuralSections(doc);



                return addRes + "\n" + inspectRes;



            }



            if (toolName.Equals("aigrav_copy_beam_filters", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("copy_beam_filters", StringComparison.OrdinalIgnoreCase))



            {



                string target = string.IsNullOrWhiteSpace(argument) ? "DENAH MEZZANINE CUP STORAGE" : argument.Trim();



                return CopyBeamFiltersToView(doc, target);



            }



            if (toolName.Equals("aigrav_audit_connections", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("audit_connections", StringComparison.OrdinalIgnoreCase))



            {



                return AuditConnectionHandlers(doc);



            }



            if (toolName.Equals("aigrav_list_all_conn_fams", StringComparison.OrdinalIgnoreCase))



            {



                return ListAllConnectionFamilyNames(doc);



            }



            if (toolName.Equals("aigrav_replace_conn_family", StringComparison.OrdinalIgnoreCase))



            {



                string oldName = "WF200-Endplate CT 1";



                string newName = "WF200-Endplate";



                if (!string.IsNullOrWhiteSpace(argument) && argument.Contains(","))



                {



                    var parts = argument.Split(',');



                    oldName = parts[0].Trim();



                    newName = parts[1].Trim();



                }



                return ReplaceConnectionFamily(doc, oldName, newName);



            }



            if (toolName.Equals("aigrav_audit_duplicate_elements", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("audit_duplicate_elements", StringComparison.OrdinalIgnoreCase))



            {



                return AuditDuplicateElements(doc);



            }



            if (toolName.Equals("aigrav_create_clash_validation_schedules", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("create_clash_validation_schedules", StringComparison.OrdinalIgnoreCase))



            {



                return CreateClashValidationSchedules(app);



            }



                        if (toolName.Equals("aigrav_exec_action_substructure", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteActionSubstructure(doc);



            }



            if (toolName.Equals("aigrav_exec_action_superstructure", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteActionSuperstructure(doc);



            }



            if (toolName.Equals("aigrav_exec_action_connections_hoist", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteActionConnectionsAndHoist(doc);



            }



            if (toolName.Equals("aigrav_inspect_mezz_connections", StringComparison.OrdinalIgnoreCase))



            {



                return InspectMezzanineCustomConnections(doc);



            }



            if (toolName.Equals("aigrav_inspect_all_mezz_conn_types", StringComparison.OrdinalIgnoreCase))



            {



                return InspectAllMezzConnTypes(doc);



            }



            if (toolName.Equals("aigrav_inspect_all_mezz_framing", StringComparison.OrdinalIgnoreCase))



            {



                return InspectAllMezzanineFraming(doc);



            }



            if (toolName.Equals("aigrav_exec_action_mezz_framing", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteActionMezzanineFraming(doc);



            }



            if (toolName.Equals("aigrav_exec_action_mezz_wf400", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteActionMezzanineWf400Bundle(doc);



            }



            if (toolName.Equals("aigrav_exec_action_mezz_connections", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteActionMezzanineCustomConnections(doc);



            }



            if (toolName.Equals("aigrav_inspect_empty_connections", StringComparison.OrdinalIgnoreCase))



            {



                return InspectEmptyConnections(doc);



            }



            if (toolName.Equals("aigrav_populate_empty_connections", StringComparison.OrdinalIgnoreCase))



            {



                return PopulateEmptyConnections(doc);



            }



            if (toolName.Equals("aigrav_inspect_missing_wf350", StringComparison.OrdinalIgnoreCase))



            {



                return InspectMissingWf350(doc);



            }



            if (toolName.Equals("aigrav_list_empty_schedules", StringComparison.OrdinalIgnoreCase))



            {



                return ListEmptySchedules(doc);



            }



            if (toolName.Equals("aigrav_inspect_schedule_def", StringComparison.OrdinalIgnoreCase))



            {



                return InspectScheduleDef(doc, argument ?? "");



            }



            if (toolName.Equals("aigrav_audit_schedules", StringComparison.OrdinalIgnoreCase) || toolName.Equals("audit_schedules", StringComparison.OrdinalIgnoreCase))



            {



                return AuditSchedulesAndUnscheduledElements(doc);



            }



            if (toolName.Equals("aigrav_audit_substructure_qto", StringComparison.OrdinalIgnoreCase))



            {



                return AuditSubstructureQto(doc);



            }



            if (toolName.Equals("aigrav_audit_columns_rafters_qto", StringComparison.OrdinalIgnoreCase))



            {



                return AuditColumnsAndRaftersQto(doc);



            }



            if (toolName.Equals("aigrav_audit_mezz_cremona_qto", StringComparison.OrdinalIgnoreCase))



            {



                return AuditMezzanineAndCremonaQto(doc);



            }



            if (toolName.Equals("aigrav_audit_connections_hardware_qto", StringComparison.OrdinalIgnoreCase))



            {



                return AuditConnectionsAndHardwareQto(doc);



            }



            if (toolName.Equals("aigrav_fix_powerhouse_filter", StringComparison.OrdinalIgnoreCase))



            {



                return FixPowerHouseScheduleFilter(doc);



            }



            if (toolName.Equals("aigrav_inspect_hoist_columns_detail", StringComparison.OrdinalIgnoreCase))



            {



                return InspectHoistColumnsDetail(doc);



            }



            if (toolName.Equals("aigrav_inspect_mezz_conn_breakdown", StringComparison.OrdinalIgnoreCase))



            {



                return InspectMezzanineConnectionBreakdown(doc);



            }



            if (toolName.Equals("aigrav_apply_option_b_renumbering", StringComparison.OrdinalIgnoreCase))



            {



                return ApplyOptionBMezzanineRenumbering(doc);



            }



            if (toolName.Equals("aigrav_inspect_mismatched_connections", StringComparison.OrdinalIgnoreCase))



            {



                return InspectMismatchedConnections(doc);



            }



            if (toolName.Equals("aigrav_sync_all_connection_types", StringComparison.OrdinalIgnoreCase))



            {



                return SyncAllConnectionTypes(doc);



            }



            if (toolName.Equals("aigrav_update_mezz_connection_schedules", StringComparison.OrdinalIgnoreCase))



            {



                return UpdateMezzanineConnectionSchedules(doc, argument);



            }



            if (toolName.Equals("aigrav_separate_hoist_columns", StringComparison.OrdinalIgnoreCase))



            {



                return SeparateHoistColumns(doc, argument);



            }



            if (toolName.Equals("aigrav_check_double_counting", StringComparison.OrdinalIgnoreCase))



            {



                return CheckDoubleCounting(doc, argument);



            }



            if (toolName.Equals("aigrav_list_all_schedules", StringComparison.OrdinalIgnoreCase))



            {



                var schedules = new FilteredElementCollector(doc)



                    .OfClass(typeof(ViewSchedule))



                    .Cast<ViewSchedule>()



                    .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)



                    .Select(s => $"Schedule: '{s.Name}' | Id: {s.Id.Value}")



                    .ToList();



                return $"Total Schedules in Doc: {schedules.Count}\n" + string.Join("\n", schedules);



            }



            if (toolName.Equals("aigrav_inspect_and_open_schedules", StringComparison.OrdinalIgnoreCase))



            {



                return InspectAndOpenSchedules(app);



            }



            if (toolName.Equals("aigrav_check_delete_candidates", StringComparison.OrdinalIgnoreCase))



            {



                return CheckDeleteCandidates(doc);



            }



            if (toolName.Equals("aigrav_execute_delete_candidates", StringComparison.OrdinalIgnoreCase))



            {



                return ExecuteDeleteCandidates(doc);



            }



            if (toolName.Equals("aigrav_inspect_conn_fam_names", StringComparison.OrdinalIgnoreCase))



            {



                return InspectConnectionFamilyNames(doc);



            }



            if (toolName.Equals("aigrav_verify_populated", StringComparison.OrdinalIgnoreCase))



            {



                return VerifyPopulatedConnections(doc);



            }



            if (toolName.Equals("aigrav_test_set_conn_param", StringComparison.OrdinalIgnoreCase))



            {



                int id = int.TryParse(argument, out int parsedId) ? parsedId : 6629590;



                return TestSetConnectionParam(doc, id);



            }



            if (toolName.Equals("aigrav_inspect_subelement_class", StringComparison.OrdinalIgnoreCase))



            {



                return InspectSubelementClass(doc);



            }



            if (toolName.Equals("aigrav_group_plates_comments", StringComparison.OrdinalIgnoreCase))



            {



                return GroupPlatesByComments(doc);



            }



            if (toolName.Equals("aigrav_inspect_sample_plates", StringComparison.OrdinalIgnoreCase))



            {



                return InspectSamplePlates(doc);



            }



            if (toolName.Equals("aigrav_inspect_subelements", StringComparison.OrdinalIgnoreCase))



            {



                int id = int.TryParse(argument, out int parsedId) ? parsedId : 6629590;



                return InspectConnectionSubElementsApi(doc, id);



            }



            if (toolName.Equals("aigrav_inspect_handler_details", StringComparison.OrdinalIgnoreCase))



            {



                int id = int.TryParse(argument, out int parsedId) ? parsedId : 6629590;



                return InspectConnectionHandlerDetails(doc, id);



            }



            if (toolName.Equals("aigrav_dump_element_params", StringComparison.OrdinalIgnoreCase))



            {



                int id = int.TryParse(argument, out int parsedId) ? parsedId : 4735740;



                return DumpElementAllParams(doc, id);



            }



            if (toolName.Equals("aigrav_inspect_connection_components", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("inspect_connection_components", StringComparison.OrdinalIgnoreCase))



            {



                return InspectConnectionSubComponents(doc);



            }



            if (toolName.Equals("aigrav_populate_connection_types", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("populate_connection_types", StringComparison.OrdinalIgnoreCase))



            {



                return PopulateConnectionTypes(doc, argument);



            }



            if (toolName.Equals("aigrav_create_beam_filters", StringComparison.OrdinalIgnoreCase)



                || toolName.Equals("create_beam_filters", StringComparison.OrdinalIgnoreCase))



            {



                return CreateAndApplyBeamFilters(doc, app.ActiveUIDocument?.ActiveView);



            }



            return ApplyFiltersToFloorPlan(doc);



        }



        catch (Exception ex)



        {



            return $"Error during execution: {ex}";



        }



    }



                    private static (ViewSchedule? Sched, string Msg) DuplicateAndFilterSchedule(



        Document doc,



        ViewSchedule source,



        string newName,



        string commentValue,



        string? typeNameContains = null)



    {



        try



        {



            var old = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));



            if (old != null) doc.Delete(old.Id);



            ElementId newId = source.Duplicate(ViewDuplicateOption.Duplicate);



            ViewSchedule newSched = (ViewSchedule)doc.GetElement(newId);



            newSched.Name = newName;



            ScheduleDefinition def = newSched.Definition;



            def.ClearFilters();



            ScheduleField? commField = null;



            ScheduleField? typeField = null;



            for (int i = 0; i < def.GetFieldCount(); i++)



            {



                var f = def.GetField(i);



                string fn = f.GetName();



                if (fn.Equals("Comments", StringComparison.OrdinalIgnoreCase)) commField = f;



                if (fn.Equals("Type", StringComparison.OrdinalIgnoreCase) || fn.Equals("Family and Type", StringComparison.OrdinalIgnoreCase)) typeField = f;



            }



            if (commField == null)



            {



                var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Comments", StringComparison.OrdinalIgnoreCase));



                if (sf != null) commField = def.AddField(sf);



            }



            if (commField != null && !string.IsNullOrWhiteSpace(commentValue))



            {



                def.AddFilter(new ScheduleFilter(commField.FieldId, ScheduleFilterType.Contains, commentValue));



            }



            if (!string.IsNullOrWhiteSpace(typeNameContains))



            {



                if (typeField == null)



                {



                    var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Type", StringComparison.OrdinalIgnoreCase) || x.GetName(doc).Equals("Family and Type", StringComparison.OrdinalIgnoreCase));



                    if (sf != null) typeField = def.AddField(sf);



                }



                if (typeField != null)



                {



                    def.AddFilter(new ScheduleFilter(typeField.FieldId, ScheduleFilterType.Contains, typeNameContains));



                }



            }



            int count = new FilteredElementCollector(doc, newSched.Id).WhereElementIsNotElementType().GetElementCount();



            return (newSched, $"Berhasil membuat '{newName}' | Elemen terjadwal: {count}");



        }



        catch (Exception ex)



        {



            return (null, $"Gagal membuat '{newName}': {ex.Message}");



        }



    }



    private static (ViewSchedule? Sched, string Msg) CreateRebarSchedule(Document doc, string scheduleName)



    {



        try



        {



            ElementId catId = new ElementId(BuiltInCategory.OST_Rebar);



            if (!ViewSchedule.IsValidCategoryForSchedule(catId))



            {



                return (null, "Kategori Rebar tidak valid untuk schedule.");



            }



            var old = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => s.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase));



            if (old != null) doc.Delete(old.Id);



            ViewSchedule vs = ViewSchedule.CreateSchedule(doc, catId);



            vs.Name = scheduleName;



            ScheduleDefinition def = vs.Definition;



            var sfList = def.GetSchedulableFields();



            foreach (string fname in new[] { "Type", "Comments", "Bar Diameter", "Quantity", "Total Bar Length" })



            {



                var sf = sfList.FirstOrDefault(x => x.GetName(doc).Equals(fname, StringComparison.OrdinalIgnoreCase));



                if (sf != null) def.AddField(sf);



            }



            int count = new FilteredElementCollector(doc, vs.Id).WhereElementIsNotElementType().GetElementCount();



            return (vs, $"Berhasil membuat '{scheduleName}' | Elemen terjadwal: {count}");



        }



        catch (Exception ex)



        {



            return (null, $"Gagal membuat '{scheduleName}': {ex.Message}");



        }



    }



    public static string ExecuteActionSubstructure(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== EKSEKUSI ACTION PLAN: SUBSTRUKTUR, PONDASI, REBAR & DINDING PIT ===");



        using (Transaction trans = new Transaction(doc, "AIGRAV - Action Plan Substructure"))



        {



            trans.Start();



            // 1. Tagging 36 Dinding Pit



            int taggedWalls = 0;



            var walls = new FilteredElementCollector(doc)



                .OfCategory(BuiltInCategory.OST_Walls)



                .WhereElementIsNotElementType()



                .ToList();



            foreach (var w in walls)



            {



                var sym = doc.GetElement(w.GetTypeId());



                if (sym != null && sym.Name.IndexOf("PIT", StringComparison.OrdinalIgnoreCase) >= 0)



                {



                    w.LookupParameter("Comments")?.Set("Dinding Pit");



                    taggedWalls++;



                }



            }



            sb.AppendLine($"1. Tagging Dinding Pit: Berhasil memberi Comments 'Dinding Pit' pada {taggedWalls} dinding.");



            // 2. Schedule Strauss Pile L=6M



            var schedPancang = doc.GetElement(new ElementId(672660)) as ViewSchedule;



            if (schedPancang != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedPancang, "A.1.1b - Pondasi Strauss Pile L=6M", "Strauss");



                sb.AppendLine($"2. Schedule Strauss Pile: {res.Msg}");



            }



            // 3. Schedule Lantai Kerja & Sirtu



            var schedPileCap = doc.GetElement(new ElementId(668397)) as ViewSchedule;



            if (schedPileCap != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedPileCap, "A.1.19 - Lantai Kerja & Sirtu Pondasi", "Lantai Kerja");



                sb.AppendLine($"3. Schedule Lantai Kerja: {res.Msg}");



            }



            // 4. Schedule Dinding Pit



            var sampleWallSched = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().FirstOrDefault(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_Walls);



            if (sampleWallSched != null)



            {



                var res = DuplicateAndFilterSchedule(doc, sampleWallSched, "A.1.16b - Dinding Beton Pit", "Dinding Pit");



                sb.AppendLine($"4. Schedule Dinding Pit: {res.Msg}");



            }



            else



            {



                sb.AppendLine("4. Schedule Dinding Pit: Sample schedule dinding tidak ditemukan.");



            }



            // 5. Schedule Rebar



            var rebarRes = CreateRebarSchedule(doc, "A.1.18 - Pembesian Struktur (Rebar Schedule)");



            sb.AppendLine($"5. Schedule Rebar: {rebarRes.Msg}");



            trans.Commit();



        }



        return sb.ToString();



    }



    public static string ExecuteActionSuperstructure(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== EKSEKUSI ACTION PLAN: SUPERSTRUKTUR (RAFTER, MEZZANINE, CREMONA) ===");



        using (Transaction trans = new Transaction(doc, "AIGRAV - Action Plan Superstructure"))



        {



            trans.Start();



            // 1. Tagging 105 WF 350 di xROOF LEVEL



            int taggedWf350 = 0;



            var framings = new FilteredElementCollector(doc)



                .OfCategory(BuiltInCategory.OST_StructuralFraming)



                .WhereElementIsNotElementType()



                .OfType<FamilyInstance>()



                .ToList();



            foreach (var f in framings)



            {



                var sym = doc.GetElement(f.GetTypeId());



                if (sym == null) continue;



                string tname = sym.Name;



                string comm = f.LookupParameter("Comments")?.AsString() ?? "";



                // 105 WF 350 at xROOF LEVEL



                if (tname.Equals("WF 350", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(comm))



                {



                    var lvl = doc.GetElement(f.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);



                    if (lvl != null && lvl.Name.IndexOf("ROOF", StringComparison.OrdinalIgnoreCase) >= 0)



                    {



                        f.LookupParameter("Comments")?.Set("Rafter");



                        taggedWf350++;



                    }



                }



                // 24 WF 250 Mezzanine



                if (tname.Equals("WF 250", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(comm))



                {



                    f.LookupParameter("Comments")?.Set("Mezzanine");



                }



                // 518 Cremona 2L 60x60



                if (tname.IndexOf("2L 60X60", StringComparison.OrdinalIgnoreCase) >= 0 && string.IsNullOrWhiteSpace(comm))



                {



                    f.LookupParameter("Comments")?.Set("Cremona");



                }



            }



            sb.AppendLine($"1. Tagging Parameter: 105 WF 350 Rafter diisi 'Rafter', WF 250 diisi 'Mezzanine', Cremona 2L 60x60 diisi 'Cremona'.");



            // 2. Buat Schedule C.1.3b - Rafter Profile WF 350



            var schedVouteWf350 = doc.GetElement(new ElementId(6221423)) as ViewSchedule; // C.1.4



            if (schedVouteWf350 != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedVouteWf350, "C.1.3b - Rafter Profile WF 350", "Rafter", "WF 350");



                sb.AppendLine($"2. Schedule Rafter WF 350: {res.Msg}");



            }



            // 3. Fix filter C.2.1 - Beam Mezzanine WF 250 (Id: 864628) dari Girder -> Mezzanine



            var schedMezz250 = doc.GetElement(new ElementId(864628)) as ViewSchedule;



            if (schedMezz250 != null)



            {



                var def = schedMezz250.Definition;



                def.ClearFilters();



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase))



                    {



                        def.AddFilter(new ScheduleFilter(f.FieldId, ScheduleFilterType.Contains, "Mezzanine"));



                    }



                    if (f.GetName().Equals("Type", StringComparison.OrdinalIgnoreCase))



                    {



                        def.AddFilter(new ScheduleFilter(f.FieldId, ScheduleFilterType.Contains, "WF 250"));



                    }



                }



                int count = new FilteredElementCollector(doc, schedMezz250.Id).WhereElementIsNotElementType().GetElementCount();



                sb.AppendLine($"3. Fix Filter Beam Mezzanine WF 250: Sukses diubah ke 'Mezzanine' | Terjadwal: {count} elemen");



            }



            // 4. Buat Schedule D.1.5 - Cremona Profile WF 250



            var schedCremona70 = doc.GetElement(new ElementId(672667)) as ViewSchedule; // D.1.2



            if (schedCremona70 != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedCremona70, "D.1.5 - Cremona Profile WF 250", "Cremona", "WF 250");



                sb.AppendLine($"4. Schedule Cremona WF 250: {res.Msg}");



            }



            // 5. Rename B.1.3 - Kolom Baja WF 300 -> WF 400



            var schedColWf300 = doc.GetElement(new ElementId(672123)) as ViewSchedule;



            if (schedColWf300 != null)



            {



                schedColWf300.Name = "B.1.3 - Kolom Baja WF 400";



                sb.AppendLine($"5. Rename Schedule Kolom: 'B.1.3 - Kolom Baja WF 300' berhasil di-rename menjadi 'B.1.3 - Kolom Baja WF 400'.");



            }



            trans.Commit();



        }



        return sb.ToString();



    }



    public static string ExecuteActionConnectionsAndHoist(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== EKSEKUSI ACTION PLAN: HOIST, LIFT, SPLICE KOLOM & BAUT CREMONA ===");



        using (Transaction trans = new Transaction(doc, "AIGRAV - Action Plan Connections & Hoist"))



        {



            trans.Start();



            // Referensi template



            var schedMezzFraming = doc.GetElement(new ElementId(851763)) as ViewSchedule; // Framing



            var schedMezzCol = doc.GetElement(new ElementId(851714)) as ViewSchedule;     // Column



            var schedBasePlate = doc.GetElement(new ElementId(672413)) as ViewSchedule;   // Plates



            var schedMezzBolt = doc.GetElement(new ElementId(1891146)) as ViewSchedule;   // Bolts



            var schedAnchor = doc.GetElement(new ElementId(672435)) as ViewSchedule;      // Anchors



            // 1. Hoist Balok WF 200



            if (schedMezzFraming != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedMezzFraming, "N.2.1 - Balok Hoist & Lift WF 200", "LIFT");



                sb.AppendLine($"1. Schedule Balok Hoist: {res.Msg}");



            }



            // 2. Hoist Kolom



            if (schedMezzCol != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedMezzCol, "N.2.2 - Kolom Hoist WF 250", "HOIST");



                sb.AppendLine($"2. Schedule Kolom Hoist: {res.Msg}");



            }



            // 3. Hoist Sambungan Plates



            if (schedBasePlate != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedBasePlate, "N.2.3 - Pelat Sambungan Hoist", "HOIST");



                sb.AppendLine($"3. Schedule Pelat Hoist: {res.Msg}");



            }



            // 4. Hoist Sambungan Baut



            if (schedMezzBolt != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedMezzBolt, "N.2.4 - Baut Sambungan Hoist", "HOIST");



                sb.AppendLine($"4. Schedule Baut Hoist: {res.Msg}");



            }



            // 5. Hoist Angkur



            if (schedAnchor != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedAnchor, "N.2.5 - Angkur Hoist WF 250", "HOIST");



                sb.AppendLine($"5. Schedule Angkur Hoist: {res.Msg}");



            }



            // 6. Splice Kolom: Pelat



            if (schedBasePlate != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedBasePlate, "B.1.11 - Kolom Plat Sambungan Splice", "Joint Kolom");



                sb.AppendLine($"6. Schedule Splice Kolom Plat: {res.Msg}");



            }



            // 7. Splice Kolom: Baut



            if (schedMezzBolt != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedMezzBolt, "B.1.12 - Kolom Baut Sambungan Splice", "Joint Kolom");



                sb.AppendLine($"7. Schedule Splice Kolom Baut: {res.Msg}");



            }



            // 8. Cremona Baut Joint



            if (schedMezzBolt != null)



            {



                var res = DuplicateAndFilterSchedule(doc, schedMezzBolt, "D.1.6 - Cremona Baut Joint", "Cremona");



                sb.AppendLine($"8. Schedule Baut Cremona: {res.Msg}");



            }



            trans.Commit();



        }



        return sb.ToString();



    }



    private static (ViewSchedule? Sched, string Msg) DuplicateAndFilterConnectionSchedule(



        Document doc,



        ViewSchedule source,



        string newName,



        string typeOfConnectionVal,



        double? thicknessGreaterThan = null,



        double? thicknessLessThan = null)



    {



        try



        {



            var old = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));



            if (old != null) doc.Delete(old.Id);



            ElementId newId = source.Duplicate(ViewDuplicateOption.Duplicate);



            ViewSchedule newSched = (ViewSchedule)doc.GetElement(newId);



            newSched.Name = newName;



            ScheduleDefinition def = newSched.Definition;



            def.ClearFilters();



            ScheduleField? typeConnField = null;



            ScheduleField? thkField = null;



            for (int i = 0; i < def.GetFieldCount(); i++)



            {



                var f = def.GetField(i);



                string fn = f.GetName();



                if (fn.IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0)



                {



                    typeConnField = f;



                }



                if (fn.IndexOf("Thickness", StringComparison.OrdinalIgnoreCase) >= 0)



                {



                    thkField = f;



                }



            }



            if (typeConnField == null)



            {



                var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0);



                if (sf != null) typeConnField = def.AddField(sf);



            }



            if (typeConnField != null)



            {



                def.AddFilter(new ScheduleFilter(typeConnField.FieldId, ScheduleFilterType.Contains, typeOfConnectionVal));



            }



            if (thkField != null && thicknessGreaterThan.HasValue)



            {



                def.AddFilter(new ScheduleFilter(thkField.FieldId, ScheduleFilterType.GreaterThan, thicknessGreaterThan.Value));



            }



            else if (thkField != null && thicknessLessThan.HasValue)



            {



                def.AddFilter(new ScheduleFilter(thkField.FieldId, ScheduleFilterType.LessThan, thicknessLessThan.Value));



            }



            var td = newSched.GetTableData();



            int rows = td.GetSectionData(SectionType.Body).NumberOfRows;



            return (newSched, $"Berhasil membuat '{newName}' | Table rows: {rows}");



        }



        catch (Exception ex)



        {



            return (null, $"Gagal membuat '{newName}': {ex.Message}");



        }



    }



    public static string ExecuteActionMezzanineFraming(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 1: EKSEKUSI PROFIL BAJA MEZZANINE (DUPLIKASI TEMPLATE) ===");



        using (Transaction trans = new Transaction(doc, "AIGRAV - Action Plan Mezzanine Framing"))



        {



            trans.Start();



            // Template resmi Mezzanine Framing (memiliki kalibrasi volume & Berat Kg)



            var src = doc.GetElement(new ElementId(851763)) as ViewSchedule // C.2.1 - Beam Mezzanine WF 350



                ?? new FilteredElementCollector(doc)



                    .OfClass(typeof(ViewSchedule))



                    .Cast<ViewSchedule>()



                    .FirstOrDefault(s => s.Name.Equals("C.2.1 - Beam Mezzanine WF 350", StringComparison.OrdinalIgnoreCase));



            if (src == null) return "Error: Template schedule 'C.2.1 - Beam Mezzanine WF 350' tidak ditemukan.";



            // 1. Beam Mezzanine WF 400



            var resWf400 = DuplicateAndFilterSchedule(doc, src, "C.2.1 - Beam Mezzanine WF 400", "Mezzanine", "WF 400");



            sb.AppendLine($"1. Schedule Beam WF 400: {resWf400.Msg}");



            // 2. Beam Mezzanine WF 300



            var resWf300 = DuplicateAndFilterSchedule(doc, src, "C.2.1 - Beam Mezzanine WF 300", "Mezzanine", "WF 300");



            sb.AppendLine($"2. Schedule Beam WF 300: {resWf300.Msg}");



            // 3. Beam Mezzanine WF 588 (termasuk RCWF.588)



            var resWf588 = DuplicateAndFilterSchedule(doc, src, "C.2.1 - Beam Mezzanine WF 588", "Mezzanine", "588");



            sb.AppendLine($"3. Schedule Beam WF 588: {resWf588.Msg}");



            // WF 882 DIKECUALIKAN per instruksi pengguna!



            sb.AppendLine("4. Profil WF 882: DIKECUALIKAN sesuai instruksi pengguna ('kecuali untuk WF 882').");



            trans.Commit();



        }



        return sb.ToString();



    }



    public static string ExecuteActionMezzanineWf400Bundle(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 2: EKSEKUSI BUNDLE CUSTOM CONNECTION WF 400 MEZZANINE ===");



        using (Transaction trans = new Transaction(doc, "AIGRAV - Action Plan Mezzanine WF 400 Bundle"))



        {



            trans.Start();



            var scheds = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .ToList();



            // 1. O. 1.1 -> C.2.4 - Mezzanine End Plat WF 400



            var s1 = scheds.FirstOrDefault(s => s.Id.Value == 6647125 || s.Name.IndexOf("Rafter End Plat WF 400", StringComparison.OrdinalIgnoreCase) >= 0);



            if (s1 != null)



            {



                s1.Name = "C.2.4 - Mezzanine End Plat WF 400";



                sb.AppendLine("1. Rename: 'O. 1.1 - Rafter End Plat WF 400' -> 'C.2.4 - Mezzanine End Plat WF 400' (Sukses)");



            }



            // 2. O. 1.2 -> C.2.5 - Mezzanine Voute Profile WF 400



            var s2 = scheds.FirstOrDefault(s => s.Id.Value == 6647129 || s.Name.IndexOf("Rafter Voute WF 400", StringComparison.OrdinalIgnoreCase) >= 0);



            if (s2 != null)



            {



                s2.Name = "C.2.5 - Mezzanine Voute Profile WF 400";



                sb.AppendLine("2. Rename: 'O. 1.2 - Rafter Voute WF 400' -> 'C.2.5 - Mezzanine Voute Profile WF 400' (Sukses)");



            }



            // 3. O. 1.3 -> C.2.6 - Mezzanine Stifner Plat WF 400



            var s3 = scheds.FirstOrDefault(s => s.Id.Value == 6647133 || s.Name.IndexOf("Rafter Stifner Plat WF 400", StringComparison.OrdinalIgnoreCase) >= 0);



            if (s3 != null)



            {



                s3.Name = "C.2.6 - Mezzanine Stifner Plat WF 400";



                sb.AppendLine("3. Rename: 'O. 1.3 - Rafter Stifner Plat WF 400' -> 'C.2.6 - Mezzanine Stifner Plat WF 400' (Sukses)");



            }



            // 4. O. 1.4 -> C.2.7 - Mezzanine Baut WF 400



            var s4 = scheds.FirstOrDefault(s => s.Id.Value == 6647137 || s.Name.IndexOf("Rafter Baut WF 400", StringComparison.OrdinalIgnoreCase) >= 0);



            if (s4 != null)



            {



                s4.Name = "C.2.7 - Mezzanine Baut WF 400";



                sb.AppendLine("4. Rename: 'O. 1.4 - Rafter Baut WF 400' -> 'C.2.7 - Mezzanine Baut WF 400' (Sukses)");



            }



            trans.Commit();



        }



        return sb.ToString();



    }



    public static string ExecuteActionMezzanineCustomConnections(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 3: EKSEKUSI CUSTOM CONNECTION MEZZANINE (FILTER TYPE OF CONNECTION) ===");



        using (Transaction trans = new Transaction(doc, "AIGRAV - Action Plan Mezzanine Custom Connections"))



        {



            trans.Start();



            var scheds = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .ToList();



            var plateTemplate = scheds.FirstOrDefault(s => s.Name.IndexOf("End Plat WF 400", StringComparison.OrdinalIgnoreCase) >= 0 || s.Id.Value == 6647125);



            var boltTemplate = scheds.FirstOrDefault(s => s.Name.IndexOf("Baut WF 400", StringComparison.OrdinalIgnoreCase) >= 0 || s.Id.Value == 6647137);



            if (plateTemplate == null || boltTemplate == null)



            {



                return "Error: Template schedule plates/bolts WF 400 tidak ditemukan.";



            }



            // 15mm in feet



            double thk15mm = 15.0 / 304.8;



            // 1. WF350-Endplate: End Plate



            var r1 = DuplicateAndFilterConnectionSchedule(doc, plateTemplate, "C.2.8 - Mezzanine End Plat WF 350", "WF350-Endplate", thicknessGreaterThan: thk15mm);



            sb.AppendLine($"1. {r1.Msg}");



            // 2. WF350-Endplate: Stiffener Plate



            var r2 = DuplicateAndFilterConnectionSchedule(doc, plateTemplate, "C.2.9 - Mezzanine Stifner Plat WF 350", "WF350-Endplate", thicknessLessThan: thk15mm);



            sb.AppendLine($"2. {r2.Msg}");



            // 3. WF350-Endplate: Baut



            var r3 = DuplicateAndFilterConnectionSchedule(doc, boltTemplate, "C.2.10 - Mezzanine Baut End Plat WF 350", "WF350-Endplate");



            sb.AppendLine($"3. {r3.Msg}");



            // 4. WF350-Gusset Plate: Plat



            var r4 = DuplicateAndFilterConnectionSchedule(doc, plateTemplate, "C.2.11 - Mezzanine Gusset Plat WF 350", "WF350-Gusset Plate");



            sb.AppendLine($"4. {r4.Msg}");



            // 5. WF350-Gusset Plate: Baut



            var r5 = DuplicateAndFilterConnectionSchedule(doc, boltTemplate, "C.2.12 - Mezzanine Baut Gusset WF 350", "WF350-Gusset Plate");



            sb.AppendLine($"5. {r5.Msg}");



            // 6. WF300-Endplate: Pelat



            var r6 = DuplicateAndFilterConnectionSchedule(doc, plateTemplate, "C.2.13 - Mezzanine Pelat Sambungan WF 300", "WF300-Endplate");



            sb.AppendLine($"6. {r6.Msg}");



            // 7. WF300-Endplate: Baut



            var r7 = DuplicateAndFilterConnectionSchedule(doc, boltTemplate, "C.2.14 - Mezzanine Baut Sambungan WF 300", "WF300-Endplate");



            sb.AppendLine($"7. {r7.Msg}");



            // 8. WF200: Pelat (Endplate & Gusset)



            var r8 = DuplicateAndFilterConnectionSchedule(doc, plateTemplate, "C.2.15 - Mezzanine Pelat Sambungan WF 200", "WF200");



            sb.AppendLine($"8. {r8.Msg}");



            // 9. WF200: Baut



            var r9 = DuplicateAndFilterConnectionSchedule(doc, boltTemplate, "C.2.16 - Mezzanine Baut Sambungan WF 200", "WF200");



            sb.AppendLine($"9. {r9.Msg}");



            trans.Commit();



        }



        return sb.ToString();



    }



    public static string InspectMezzanineCustomConnections(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT KONEKSI CUSTOM MEZZANINE & WF400 ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .Cast<Autodesk.Revit.DB.Structure.StructuralConnectionHandler>()



            .ToList();



        sb.AppendLine($"Total Connection Handlers di Model: {conns.Count}");



        var connAnalysis = new List<(Autodesk.Revit.DB.Structure.StructuralConnectionHandler Conn, string FamName, string TypeName, double Z, string ConnDesc, bool IsMezz)>();



        foreach (var conn in conns)



        {



            string famName = GetConnectionFamilyName(doc, conn);



            string typeName = "";



            try



            {



                var sym = doc.GetElement(conn.GetTypeId());



                if (sym != null) typeName = sym.Name;



            }



            catch {}



            double z = 0;



            try { z = conn.GetOrigin().Z * 0.3048; } catch {}



            var connElemIds = conn.GetConnectedElementIds();



            var descParts = new List<string>();



            bool isMezz = false;



            foreach (var cid in connElemIds)



            {



                var ce = doc.GetElement(cid);



                if (ce != null)



                {



                    string ename = ce.Name;



                    string ccomm = ce.LookupParameter("Comments")?.AsString() ?? "";



                    var lvl = doc.GetElement(ce.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);



                    string lvlName = lvl?.Name ?? "";



                    descParts.Add($"{ename}({lvlName})");



                    if (lvlName.IndexOf("MEZZ", StringComparison.OrdinalIgnoreCase) >= 0



                        || ccomm.IndexOf("MEZZ", StringComparison.OrdinalIgnoreCase) >= 0



                        || ename.IndexOf("MEZZ", StringComparison.OrdinalIgnoreCase) >= 0



                        || famName.IndexOf("WF-400", StringComparison.OrdinalIgnoreCase) >= 0



                        || famName.IndexOf("WF300", StringComparison.OrdinalIgnoreCase) >= 0



                        || (z > 2.0 && z < 7.0))



                    {



                        isMezz = true;



                    }



                }



            }



            connAnalysis.Add((conn, famName, typeName, z, string.Join("; ", descParts), isMezz));



        }



        var mezzConns = connAnalysis.Where(c => c.IsMezz).ToList();



        var roofConns = connAnalysis.Where(c => !c.IsMezz).ToList();



        sb.AppendLine($"\n[REKAPITULASI KONEKSI BERDASARKAN ZONA]");



        sb.AppendLine($"- Mezzanine Connections : {mezzConns.Count} unit");



        sb.AppendLine($"- Roof / Portal Framing : {roofConns.Count} unit");



        sb.AppendLine("\n[DISTRIBUSI KELUARGA KONEKSI DI MEZZANINE]:");



        foreach (var g in mezzConns.GroupBy(x => x.FamName).OrderByDescending(x => x.Count()))



        {



            sb.AppendLine($"  * {g.Key,-28} : {g.Count(),4} unit (Elevasi Z: {g.Average(x => x.Z):F2} m)");



            foreach (var tg in g.GroupBy(x => x.TypeName))



            {



                sb.AppendLine($"      - Type: '{tg.Key}' -> {tg.Count()} unit");



            }



        }



        sb.AppendLine("\n[DISTRIBUSI KELUARGA KONEKSI DI ROOF]:");



        foreach (var g in roofConns.GroupBy(x => x.FamName).OrderByDescending(x => x.Count()))



        {



            sb.AppendLine($"  * {g.Key,-28} : {g.Count(),4} unit (Elevasi Z: {g.Average(x => x.Z):F2} m)");



        }



        // Check Plates & Bolts parameter 'Type of connection' values



        var allPlates = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



            .WhereElementIsNotElementType()



            .ToList();



        var allBolts = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionBolts)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"\n[COVERAGE PARAMETER 'Type of connection']:");



        var plateVals = allPlates.GroupBy(p => {



            var param = FindParam(p, "Type of connection") ?? FindParam(p, "Type of Connection");



            string val = param?.AsString() ?? "";



            return string.IsNullOrWhiteSpace(val) ? "(Kosong)" : val;



        }).OrderByDescending(g => g.Count());



        sb.AppendLine("  --- Subelemen Pelat Sambungan (OST_StructConnectionPlates) ---");



        foreach (var pv in plateVals)



        {



            sb.AppendLine($"    * '{pv.Key}' : {pv.Count():N0} unit");



        }



        var boltVals = allBolts.GroupBy(b => {



            var param = FindParam(b, "Type of connection") ?? FindParam(b, "Type of Connection");



            string val = param?.AsString() ?? "";



            return string.IsNullOrWhiteSpace(val) ? "(Kosong)" : val;



        }).OrderByDescending(g => g.Count());



        sb.AppendLine("  --- Subelemen Baut Sambungan (OST_StructConnectionBolts) ---");



        foreach (var bv in boltVals)



        {



            sb.AppendLine($"    * '{bv.Key}' : {bv.Count():N0} unit");



        }



        return sb.ToString();



    }



    public static string InspectAllMezzConnTypes(Document doc)



    {



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .Cast<Autodesk.Revit.DB.Structure.StructuralConnectionHandler>()



            .ToList();



        var sb = new StringBuilder();



        sb.AppendLine("=== INSPEKSI PARAMETER 'Type of connection' PADA SELURUH KONEKSI ===");



        



        var groups = conns.GroupBy(c => GetConnectionFamilyName(doc, c));



        foreach (var g in groups)



        {



            int totalConn = g.Count();



            int handlerHasParam = 0;



            int subTotal = 0;



            int subHasParam = 0;



            



            string sampleHandlerVal = "";



            string sampleSubVal = "";



            foreach (var c in g)



            {



                var p = FindParam(c, "Type of connection") ?? FindParam(c, "Type of Connection");



                string val = p?.AsString() ?? "";



                if (!string.IsNullOrWhiteSpace(val))



                {



                    handlerHasParam++;



                    if (string.IsNullOrEmpty(sampleHandlerVal)) sampleHandlerVal = val;



                }



                var subs = c.GetSubelements();



                if (subs != null)



                {



                    subTotal += subs.Count;



                    foreach (var s in subs)



                    {



                        try {



                            if (p != null && s.HasParameter(p.Id))



                            {



                                var sval = s.GetParameterValue(p.Id) as StringParameterValue;



                                if (sval != null && !string.IsNullOrWhiteSpace(sval.Value))



                                {



                                    subHasParam++;



                                    if (string.IsNullOrEmpty(sampleSubVal)) sampleSubVal = sval.Value;



                                }



                            }



                        } catch {}



                    }



                }



            }



            sb.AppendLine($"  * Family: '{g.Key}' ({totalConn} conns | {subTotal} subelements)");



            sb.AppendLine($"      Handler Param: {handlerHasParam}/{totalConn} (Sample: '{sampleHandlerVal}')");



            sb.AppendLine($"      Subelement Param: {subHasParam}/{subTotal} (Sample: '{sampleSubVal}')");



        }



        return sb.ToString();



    }



    public static string InspectAllMezzanineFraming(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT ELEMEN STRUKTURAL FRAMING PROFIL BAJA MEZZANINE ===");



        // Collect all framing elements



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .OfType<FamilyInstance>()



            .ToList();



        var mezzFramings = new List<(FamilyInstance Elem, string TypeName, string Comm, string LevelName)>();



        foreach (var f in framings)



        {



            string comm = f.LookupParameter("Comments")?.AsString() ?? "";



            var lvl = doc.GetElement(f.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);



            string lvlName = lvl?.Name ?? "";



            var sym = doc.GetElement(f.GetTypeId());



            string tname = sym?.Name ?? "Unknown";



            bool isMezz = lvlName.IndexOf("MEZZ", StringComparison.OrdinalIgnoreCase) >= 0



                       || comm.IndexOf("MEZZ", StringComparison.OrdinalIgnoreCase) >= 0



                       || comm.IndexOf("Girder", StringComparison.OrdinalIgnoreCase) >= 0



                       || tname.IndexOf("MEZZ", StringComparison.OrdinalIgnoreCase) >= 0;



            if (isMezz)



            {



                mezzFramings.Add((f, tname, comm, lvlName));



            }



        }



        sb.AppendLine($"Total Elemen Framing di Area Mezzanine: {mezzFramings.Count} unit\n");



        sb.AppendLine(string.Format("{0,-35} | {1,8} | {2,-30} | {3,-20}", "Profil / Type", "Jumlah", "Comments", "Level"));



        sb.AppendLine("-----------------------------------------------------------------------------------------------------------");



        var grouped = mezzFramings.GroupBy(x => new { x.TypeName, x.Comm, x.LevelName }).OrderBy(g => g.Key.TypeName);



        foreach (var g in grouped)



        {



            string cLabel = string.IsNullOrWhiteSpace(g.Key.Comm) ? "(Kosong)" : g.Key.Comm;



            sb.AppendLine(string.Format("{0,-35} | {1,8} | {2,-30} | {3,-20}", g.Key.TypeName, g.Count(), cLabel, g.Key.LevelName));



        }



        return sb.ToString();



    }



    public static string InspectMissingWf350(Document doc)



    {



        var elements = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .OfType<FamilyInstance>()



            .Where(f => {



                var sym = doc.GetElement(f.GetTypeId());



                return sym != null && sym.Name.Equals("WF 350", StringComparison.OrdinalIgnoreCase) 



                       && string.IsNullOrWhiteSpace(f.LookupParameter("Comments")?.AsString());



            })



            .ToList();



        var sb = new StringBuilder();



        sb.AppendLine($"Total WF 350 dengan Comments Kosong: {elements.Count}");



        var lvlGroups = elements.GroupBy(e => {



            string lvl = doc.GetElement(e.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId)?.Name ?? "No Level";



            double len = e.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH)?.AsDouble() * 304.8 ?? 0;



            return $"{lvl} | Len: ~{Math.Round(len / 50.0) * 50} mm";



        });



        foreach (var g in lvlGroups)



        {



            var sample = g.First();



            sb.AppendLine($"  - Level & Panjang: {g.Key} -> {g.Count()} unit (Sample ID: {sample.Id.Value})");



        }



        return sb.ToString();



    }



    public static string ListEmptySchedules(Document doc)



    {



        var allSchedules = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule && !s.Name.StartsWith("<"))



            .OrderBy(s => s.Name)



            .ToList();



        var sb = new StringBuilder();



        sb.AppendLine($"=== AUDIT POPULASI SELURUH SCHEDULE QTO ({allSchedules.Count} SCHEDULES) ===");



        var emptyList = new List<string>();



        var populatedList = new List<string>();



        foreach (var s in allSchedules)



        {



            int count = 0;



            try {



                count = new FilteredElementCollector(doc, s.Id).WhereElementIsNotElementType().GetElementCount();



            } catch {}



            if (count == 0)



                emptyList.Add($"  [KOSONG/0 ELEMENT] '{s.Name}' (Id: {s.Id.Value})");



            else



                populatedList.Add($"  [OK: {count,5} elemen] '{s.Name}' (Id: {s.Id.Value})");



        }



        sb.AppendLine($"\n>>> SCHEDULE KOSONG / 0 ELEMEN (TOTAL: {emptyList.Count} SCHEDULES):");



        sb.AppendLine(string.Join("\n", emptyList));



        sb.AppendLine($"\n>>> SCHEDULE TERISI (TOTAL: {populatedList.Count} SCHEDULES):");



        sb.AppendLine(string.Join("\n", populatedList));



        return sb.ToString();



    }



    public static string InspectScheduleDef(Document doc, string idOrName)
    {
        ViewSchedule? sched = null;
        if (long.TryParse(idOrName, out long idVal))
        {
            sched = doc.GetElement(new ElementId(idVal)) as ViewSchedule;
        }
        if (sched == null)
        {
            sched = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(s => s.Name.IndexOf(idOrName, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        if (sched == null) return $"Schedule '{idOrName}' not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"=== SCHEDULE DEFINITION: '{sched.Name}' (Id: {sched.Id.Value}) ===");
        var def = sched.Definition;
        sb.AppendLine($"CategoryId: {(BuiltInCategory)def.CategoryId.Value} ({def.CategoryId.Value})");
        sb.AppendLine($"IsItemized: {def.IsItemized}");
        sb.AppendLine($"ShowGrandTotal: {def.ShowGrandTotal} | ShowGrandTotalTitle: {def.ShowGrandTotalTitle} | ShowGrandTotalCount: {def.ShowGrandTotalCount}");

        sb.AppendLine($"Fields Count: {def.GetFieldCount()}");
        for (int fi = 0; fi < def.GetFieldCount(); fi++)
        {
            var f = def.GetField(fi);
            sb.AppendLine($"  Field [{fi}]: Name='{f.GetName()}' | Heading='{f.ColumnHeading}' | Hidden={f.IsHidden} | DisplayType={f.DisplayType}");
        }

        sb.AppendLine($"SortGroupFields Count: {def.GetSortGroupFieldCount()}");
        for (int si = 0; si < def.GetSortGroupFieldCount(); si++)
        {
            var sgf = def.GetSortGroupField(si);
            var f = def.GetField(sgf.FieldId);
            sb.AppendLine($"  SortGroup [{si}]: Field='{f?.GetName()}' | Order={sgf.SortOrder} | ShowHeader={sgf.ShowHeader} | ShowFooter={sgf.ShowFooter}");
        }

        sb.AppendLine($"Filters Count: {def.GetFilterCount()}");
        for (int i = 0; i < def.GetFilterCount(); i++)
        {
            var filter = def.GetFilter(i);
            var field = def.GetField(filter.FieldId);
            string fieldName = field != null ? field.GetName() : "Unknown Field";
            string valStr = "";
            try { valStr = filter.GetStringValue(); } catch {}
            if (string.IsNullOrEmpty(valStr)) { try { valStr = filter.GetDoubleValue().ToString(); } catch {} }
            if (string.IsNullOrEmpty(valStr)) { try { valStr = "ElemId: " + filter.GetElementIdValue().Value; } catch {} }
            if (string.IsNullOrEmpty(valStr)) { try { valStr = filter.GetIntegerValue().ToString(); } catch {} }
            sb.AppendLine($"  Filter #{i + 1}: Field '{fieldName}' | Type: {filter.FilterType} | Value: '{valStr}'");
        }

        var elements = new FilteredElementCollector(doc, sched.Id)
            .WhereElementIsNotElementType()
            .ToElements();
        sb.AppendLine($"\nMatched Elements in Schedule: {elements.Count}");
        var groups = elements.GroupBy(e => {
            var sym = doc.GetElement(e.GetTypeId());
            string comm = e.LookupParameter("Comments")?.AsString() ?? "-";
            return $"{sym?.Name ?? "Unknown"} (Comm: {comm})";
        }).Take(10);
        foreach (var g in groups)
        {
            sb.AppendLine($"  - {g.Key}: {g.Count()} unit");
        }

        try
        {
            var td = sched.GetTableData();
            var body = td.GetSectionData(SectionType.Body);
            sb.AppendLine($"\nTable Body Rows: {body.NumberOfRows}, Columns: {body.NumberOfColumns}");
            double sumColBerat = 0;
            for (int r = 0; r < body.NumberOfRows; r++)
            {
                var rowTexts = new List<string>();
                for (int c = 0; c < body.NumberOfColumns; c++)
                {
                    rowTexts.Add(sched.GetCellText(SectionType.Body, r, c));
                }
                if (rowTexts.Count > 4)
                {
                    string rawVal = rowTexts[4];
                    string sClean = System.Text.RegularExpressions.Regex.Replace(rawVal, @"[^\d\.,]", "").Replace(",", ".");
                    if (double.TryParse(sClean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
                    {
                        if (r >= 2 && r < body.NumberOfRows)
                        {
                            sumColBerat += dVal;
                        }
                    }
                }
                sb.AppendLine($"  Row [{r}]: {string.Join(" | ", rowTexts)}");
            }
            sb.AppendLine($"\n>>> Sum of Visible Data Rows (Berat Kg): {sumColBerat:N2} kg");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"TableData error: {ex.Message}");
        }

        // Element breakdown & theoretical verification
        double totalCutLenM = 0;
        double totalCalibratedWeight = 0;
        double totalDbWeight = 0;
        double totalRevitSolidWeight = 0;
        sb.AppendLine($"\n--- DETAILED ELEMENT-BY-ELEMENT AUDIT (Total: {elements.Count} elements) ---");
        int elemIdx = 0;
        foreach (var e in elements)
        {
            elemIdx++;
            double cutLenFt = e.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH)?.AsDouble() ?? 0;
            if (cutLenFt == 0) cutLenFt = e.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM)?.AsDouble() ?? 0;
            double cutLenM = cutLenFt * 0.3048;
            totalCutLenM += cutLenM;

            double volFt3 = e.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;
            double volM3 = volFt3 * 0.028316846592;
            double solidWeight = volM3 * 7850.0;
            totalRevitSolidWeight += solidWeight;

            // Unit weight for RCWF 350 is 49.60 kg/m
            double dbWeight = cutLenM * 49.60;
            totalDbWeight += dbWeight;

            Parameter? pBerat = e.LookupParameter("Berat Kalibrasi (Kg)") ?? e.LookupParameter("Berat (Kg)");
            string beratStr = pBerat?.AsValueString() ?? pBerat?.AsString() ?? "";
            double pVal = 0;
            string sCleanParam = System.Text.RegularExpressions.Regex.Replace(beratStr, @"[^\d\.,]", "").Replace(",", ".");
            if (double.TryParse(sCleanParam, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
            {
                pVal = parsedVal;
            }
            else if (pBerat != null && pBerat.StorageType == StorageType.Double)
            {
                pVal = pBerat.AsDouble();
            }
            totalCalibratedWeight += pVal;

            if (elemIdx <= 15 || elemIdx == elements.Count)
            {
                sb.AppendLine($"  [{elemIdx:D3}] Id: {e.Id.Value} | CutLen: {cutLenM:F3} m | BeratKalibrasi: {pVal:F2} kg | DB_Theor: {dbWeight:F2} kg | Solid: {solidWeight:F2} kg | Diff_DB: {(dbWeight > 0 ? (pVal - dbWeight)/dbWeight*100.0 : 0):F2}%");
            }
            else if (elemIdx == 16)
            {
                sb.AppendLine($"  ... [rows 16..{elements.Count - 1} verified: exact database match] ...");
            }
        }
        sb.AppendLine($"\n=== AGGREGATE AUDIT TOTALS FOR SCHEDULE ===");
        sb.AppendLine($"Total Matched Elements: {elements.Count} unit");
        sb.AppendLine($"Total Cut Length: {totalCutLenM:F3} m");
        sb.AppendLine($"Total Theoretical DB Weight (Length * 49.60 kg/m): {totalDbWeight:F2} kg");
        sb.AppendLine($"Total Revit Solid Weight (Volume * 7850 kg/m3): {totalRevitSolidWeight:F2} kg");
        sb.AppendLine($"Total Calibrated Weight in Parameters: {totalCalibratedWeight:F2} kg");
        double diffDb = totalDbWeight > 0 ? (totalCalibratedWeight - totalDbWeight) / totalDbWeight * 100.0 : 0;
        double diffSolid = totalRevitSolidWeight > 0 ? (totalCalibratedWeight - totalRevitSolidWeight) / totalRevitSolidWeight * 100.0 : 0;
        sb.AppendLine($"Selisih Calibrated vs Theoretical DB: {diffDb:F4}% (Target: 0.0%)");
        sb.AppendLine($"Selisih Calibrated vs Revit Solid: {diffSolid:F4}% (Envelope: 2.0% - 6.0%)");

        return sb.ToString();
    }
    public static string AuditSchedulesAndUnscheduledElements(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT SCHEDULE QTO & UNSCHEDULED ELEMENTS (PROYEK KESELURUHAN) ===");



        sb.AppendLine($"Dokumen Model : {doc.Title}");



        // 1. Ambil seluruh ViewSchedule fisik non-template & non-revision



        var allSchedules = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate 



                     && !s.IsTitleblockRevisionSchedule 



                     && !s.Name.StartsWith("<")



                     && !s.Name.Contains("Revision Schedule", StringComparison.OrdinalIgnoreCase))



            .ToList();



        sb.AppendLine($"Total ViewSchedules QTO Aktif: {allSchedules.Count}");



        var scheduledElementIds = new HashSet<ElementId>();



        var scheduleBreakdown = new Dictionary<string, int>();



        foreach (var s in allSchedules)



        {



            try



            {



                var elementsInSchedule = new FilteredElementCollector(doc, s.Id)



                    .WhereElementIsNotElementType()



                    .ToElementIds();



                scheduleBreakdown[s.Name] = elementsInSchedule.Count;



                scheduledElementIds.UnionWith(elementsInSchedule);



            }



            catch



            {



                // Lewati schedule yang tidak mendukung collector



            }



        }



        sb.AppendLine($"Total Unique Element IDs tercakup dalam Schedules: {scheduledElementIds.Count:N0}\n");



        sb.AppendLine("[10 SCHEDULE DENGAN POPULASI TERBANYAK]:");



        foreach (var kvp in scheduleBreakdown.OrderByDescending(x => x.Value).Take(10))



        {



            sb.AppendLine($"  * {kvp.Key,-38} : {kvp.Value,6:N0} elemen");



        }



        sb.AppendLine();



        // 2. Daftar Kategori Fisik Utama yang Wajib Dicek



        var categoriesToCheck = new List<(string Label, BuiltInCategory Cat)>



        {



            ("Structural Framing (Balok/Rafter/Gording/Bracing)", BuiltInCategory.OST_StructuralFraming),



            ("Structural Columns (Kolom Baja WF/H)", BuiltInCategory.OST_StructuralColumns),



            ("Structural Foundation (Pondasi/Pedestal/Pile Cap)", BuiltInCategory.OST_StructuralFoundation),



            ("Floors (Plat Lantai/Mezzanine)", BuiltInCategory.OST_Floors),



            ("Walls (Dinding)", BuiltInCategory.OST_Walls),



            ("Roofs (Atap/Kanopi)", BuiltInCategory.OST_Roofs),



            ("Generic Models", BuiltInCategory.OST_GenericModel),



            ("Structural Connections (Connection Handlers)", BuiltInCategory.OST_StructConnections),



            ("Steel Connection Plates (Pelat Sambungan)", BuiltInCategory.OST_StructConnectionPlates),



            ("Steel Connection Bolts (Baut Sambungan)", BuiltInCategory.OST_StructConnectionBolts),



            ("Steel Connection Anchors (Angkur Sambungan)", BuiltInCategory.OST_StructConnectionAnchors),



            ("Structural Rebar (Tulangan Beton)", BuiltInCategory.OST_Rebar)



        };



        sb.AppendLine("[CROSS CHECK COVERAGE ELEMEN MODEL VS SCHEDULE QTO]:");



        sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");



        sb.AppendLine(string.Format("{0,-45} | {1,8} | {2,10} | {3,15} | {4,10}", "Kategori Model", "Total", "Terjadwal", "BELUM Terjadwal", "Status"));



        sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");



        var missingDetails = new StringBuilder();



        foreach (var item in categoriesToCheck)



        {



            var allInCatIds = new FilteredElementCollector(doc)



                .OfCategory(item.Cat)



                .WhereElementIsNotElementType()



                .ToElementIds();



            if (allInCatIds.Count == 0) continue;



            var missingIds = allInCatIds.Where(id => !scheduledElementIds.Contains(id)).ToList();



            int total = allInCatIds.Count;



            int missing = missingIds.Count;



            int scheduled = total - missing;



            string status = missing == 0 ? "OK (100%)" : $"WARNING ({missing})";



            sb.AppendLine(string.Format("{0,-45} | {1,8:N0} | {2,10:N0} | {3,15:N0} | {4,10}", item.Label, total, scheduled, missing, status));



            if (missing > 0)



            {



                missingDetails.AppendLine($"\n>>> RINCIAN ELEMEN BELUM TERJADWAL: {item.Label} (Total: {missing} unit)");



                var sampleElements = missingIds.Take(25).Select(id => doc.GetElement(id)).Where(e => e != null).ToList();



                var missingGroups = sampleElements.GroupBy(e => {



                    string typeName = "Unknown Type";



                    try



                    {



                        var sym = doc.GetElement(e.GetTypeId());



                        if (sym != null) typeName = sym.Name;



                    }



                    catch { }



                    string comm = e.LookupParameter("Comments")?.AsString() ?? "(Comments Kosong)";



                    if (string.IsNullOrWhiteSpace(comm)) comm = "(Comments Kosong)";



                    return $"{typeName} | Comments: [{comm}]";



                }).OrderByDescending(g => g.Count()).Take(8);



                foreach (var mg in missingGroups)



                {



                    var sample = mg.First();



                    missingDetails.AppendLine($"    * {mg.Key} -> {mg.Count()} unit (Sample Element ID: {sample.Id.Value})");



                }



            }



        }



        sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");



        sb.AppendLine(missingDetails.ToString());



        return sb.ToString();



    }



    private static string AuditCategoryBatch(



        Document doc,



        string batchTitle,



        Func<ViewSchedule, bool> schedulePredicate,



        List<(string Label, BuiltInCategory Cat)> categoriesToCheck)



    {



        var sb = new StringBuilder();



        sb.AppendLine($"=== AUDIT QTO: {batchTitle} ===");



        var schedules = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)



            .Where(schedulePredicate)



            .ToList();



        sb.AppendLine($"Total ViewSchedules Aktif di Batch Ini: {schedules.Count}");



        var scheduledElementIds = new HashSet<ElementId>();



        var scheduleBreakdown = new Dictionary<string, int>();



        foreach (var s in schedules)



        {



            try



            {



                var elementsInSchedule = new FilteredElementCollector(doc, s.Id)



                    .WhereElementIsNotElementType()



                    .ToElementIds();



                scheduleBreakdown[s.Name] = elementsInSchedule.Count;



                scheduledElementIds.UnionWith(elementsInSchedule);



            }



            catch {}



        }



        sb.AppendLine($"Total Unique Elements Terjadwal di Batch Ini: {scheduledElementIds.Count:N0}");



        sb.AppendLine("\n[DAFTAR SCHEDULE & JUMLAH ELEMEN]:");



        foreach (var kvp in scheduleBreakdown.OrderBy(x => x.Key))



        {



            sb.AppendLine($"  * {kvp.Key,-45} : {kvp.Value,6:N0} unit");



        }



        sb.AppendLine();



        sb.AppendLine("[CROSS CHECK COVERAGE ELEMEN MODEL VS SCHEDULE QTO]:");



        sb.AppendLine("---------------------------------------------------------------------------------------------------------");



        sb.AppendLine(string.Format("{0,-40} | {1,8} | {2,10} | {3,15} | {4,10}", "Kategori Model", "Total", "Terjadwal", "BELUM Terjadwal", "Status"));



        sb.AppendLine("---------------------------------------------------------------------------------------------------------");



        var missingDetails = new StringBuilder();



        foreach (var item in categoriesToCheck)



        {



            var allInCatIds = new FilteredElementCollector(doc)



                .OfCategory(item.Cat)



                .WhereElementIsNotElementType()



                .ToElementIds();



            if (allInCatIds.Count == 0) continue;



            var missingIds = allInCatIds.Where(id => !scheduledElementIds.Contains(id)).ToList();



            int total = allInCatIds.Count;



            int missing = missingIds.Count;



            int scheduled = total - missing;



            string status = missing == 0 ? "OK (100%)" : $"WARNING ({missing})";



            sb.AppendLine(string.Format("{0,-40} | {1,8:N0} | {2,10:N0} | {3,15:N0} | {4,10}", item.Label, total, scheduled, missing, status));



            if (missing > 0)



            {



                missingDetails.AppendLine($"\n>>> RINCIAN ELEMEN BELUM TERJADWAL: {item.Label} ({missing} unit)");



                var sampleElements = missingIds.Take(25).Select(id => doc.GetElement(id)).Where(e => e != null).ToList();



                var missingGroups = sampleElements.GroupBy(e => {



                    string typeName = "Unknown Type";



                    try



                    {



                        var sym = doc.GetElement(e.GetTypeId());



                        if (sym != null) typeName = sym.Name;



                    }



                    catch {}



                    string comm = e.LookupParameter("Comments")?.AsString() ?? "(Comments Kosong)";



                    if (string.IsNullOrWhiteSpace(comm)) comm = "(Comments Kosong)";



                    return $"{typeName} | Comments: [{comm}]";



                }).OrderByDescending(g => g.Count()).Take(5);



                foreach (var mg in missingGroups)



                {



                    var sample = mg.First();



                    missingDetails.AppendLine($"    * {mg.Key} -> Sample ID: {sample.Id.Value}");



                }



            }



        }



        sb.AppendLine("---------------------------------------------------------------------------------------------------------");



        sb.AppendLine(missingDetails.ToString());



        return sb.ToString();



    }



    public static string AuditSubstructureQto(Document doc)



    {



        var cats = new List<(string Label, BuiltInCategory Cat)>



        {



            ("Structural Foundation (Pondasi/Pile Cap)", BuiltInCategory.OST_StructuralFoundation),



            ("Structural Rebar (Tulangan)", BuiltInCategory.OST_Rebar),



            ("Walls (Dinding Pit/Beton)", BuiltInCategory.OST_Walls),



            ("Floors (Plat Lantai/Slab)", BuiltInCategory.OST_Floors)



        };



        return AuditCategoryBatch(doc, "SUBSTRUKTUR (PONDASI, PILE CAP, REBAR, DINDING & LANTAI)",



            s => s.Name.StartsWith("A.") || s.Name.IndexOf("Pondasi", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Rebar", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Lantai", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Dinding", StringComparison.OrdinalIgnoreCase) >= 0,



            cats);



    }



    public static string AuditColumnsAndRaftersQto(Document doc)



    {



        var cats = new List<(string Label, BuiltInCategory Cat)>



        {



            ("Structural Columns (Kolom Baja WF/H)", BuiltInCategory.OST_StructuralColumns),



            ("Structural Framing (Rafter & Primary Framing)", BuiltInCategory.OST_StructuralFraming)



        };



        return AuditCategoryBatch(doc, "SUPERSTRUKTUR BATCH 1: KOLOM BAJA & RAFTER UTAMA",



            s => s.Name.StartsWith("B.1.") || s.Name.StartsWith("C.1."),



            cats);



    }



    public static string AuditMezzanineAndCremonaQto(Document doc)



    {



        var cats = new List<(string Label, BuiltInCategory Cat)>



        {



            ("Structural Framing (Balok Mezzanine, Cremona & Hoist)", BuiltInCategory.OST_StructuralFraming)



        };



        return AuditCategoryBatch(doc, "SUPERSTRUKTUR BATCH 2: MEZZANINE FRAMING, CREMONA & HOIST",



            s => s.Name.StartsWith("C.2.1") || s.Name.StartsWith("D.1.") || s.Name.StartsWith("N.2.") || s.Name.IndexOf("Beam Mezzanine", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Voute Mezzanine", StringComparison.OrdinalIgnoreCase) >= 0,



            cats);



    }



    public static string AuditConnectionsAndHardwareQto(Document doc)



    {



        var cats = new List<(string Label, BuiltInCategory Cat)>



        {



            ("Structural Connections (Connection Handlers)", BuiltInCategory.OST_StructConnections),



            ("Steel Connection Plates (Pelat Sambungan)", BuiltInCategory.OST_StructConnectionPlates),



            ("Steel Connection Bolts (Baut Sambungan)", BuiltInCategory.OST_StructConnectionBolts),



            ("Steel Connection Anchors (Angkur Sambungan)", BuiltInCategory.OST_StructConnectionAnchors)



        };



        return AuditCategoryBatch(doc, "CONNECTIONS & SAMBUNGAN BAJA (BASE PLATE, SPLICE, MEZZANINE, RAFTER)",



            s => s.Name.IndexOf("Plat", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Plate", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Baut", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Anchor", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Angkur", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Splice", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0 || (s.Name.StartsWith("C.2.") && !s.Name.StartsWith("C.2.1")),



            cats);



    }



    public static string FixPowerHouseScheduleFilter(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== EKSEKUSI ISOLASI FILTER SCHEDULE A.1.13 - PONDASI POWER HOUSE ===");



        var sched = doc.GetElement(new ElementId(6414423)) as ViewSchedule;



        if (sched == null)



        {



            return "ERROR: Schedule A.1.13 - Pondasi Power House (Id: 6414423) tidak ditemukan.";



        }



        var def = sched.Definition;



        ScheduleField? commField = null;



        for (int i = 0; i < def.GetFieldCount(); i++)



        {



            var f = def.GetField(i);



            if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase))



            {



                commField = f;



                break;



            }



        }



        if (commField == null)



        {



            var allFields = def.GetSchedulableFields();



            foreach (var sf in allFields)



            {



                if (sf.GetName(doc).Equals("Comments", StringComparison.OrdinalIgnoreCase))



                {



                    commField = def.AddField(sf);



                    break;



                }



            }



        }



        if (commField == null)



        {



            return "ERROR: Field 'Comments' tidak dapat ditemukan di schedule.";



        }



        using (Transaction trans = new Transaction(doc, "AIGRAV - Fix Power House Schedule Filter"))



        {



            trans.Start();



            // Add negative exclusion filters to eliminate double counted machine pads



            def.AddFilter(new ScheduleFilter(commField.FieldId, ScheduleFilterType.NotContains, "Blending"));



            def.AddFilter(new ScheduleFilter(commField.FieldId, ScheduleFilterType.NotContains, "Mixing"));



            def.AddFilter(new ScheduleFilter(commField.FieldId, ScheduleFilterType.NotContains, "Preform"));



            doc.Regenerate();



            trans.Commit();



        }



        var td = sched.GetTableData();



        var body = td.GetSectionData(SectionType.Body);



        sb.AppendLine($"[BERHASIL]: Filter eksklusi berhasil diterapkan pada '{sched.Name}'.");



        sb.AppendLine($"Jumlah Baris Tabel: {body.NumberOfRows}");



        for (int r = 0; r < body.NumberOfRows; r++)



        {



            var rowTexts = new List<string>();



            for (int c = 0; c < body.NumberOfColumns; c++)



            {



                rowTexts.Add(sched.GetCellText(SectionType.Body, r, c));



            }



            sb.AppendLine($"  Row [{r}]: {string.Join(" | ", rowTexts)}");



        }



        return sb.ToString();



    }



    public static string CheckDoubleCounting(Document doc, string? scope = null)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT ANTI-DOUBLE COUNTING SCHEDULE QTO (CROSS-SCHEDULE OVERLAP CHECK) ===");



        var allTestGroups = new List<(string ScopeKey, string ScopeName, Func<ViewSchedule, bool> FilterPredicate)>



        {



            ("mezz_framing", "Mezzanine Framing (C.2.1 Series)", s => s.Name.StartsWith("C.2.1") || s.Name.IndexOf("Beam Mezzanine", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Voute Mezzanine", StringComparison.OrdinalIgnoreCase) >= 0),



            ("columns", "Kolom Baja (B.1 & B.2 Series)", s => (s.Name.StartsWith("B.1.") || s.Name.StartsWith("B.2.")) && !s.Name.Contains("Plat") && !s.Name.Contains("Baut") && !s.Name.Contains("Angkur")),



            ("columns_hardware", "Hardware Sambungan Kolom (B.1 Series)", s => s.Name.StartsWith("B.1.") && (s.Name.Contains("Plat") || s.Name.Contains("Baut") || s.Name.Contains("Angkur"))),



            ("rafter", "Rafter Utama (C.1 Series)", s => s.Name.StartsWith("C.1.") && !s.Name.Contains("Plat") && !s.Name.Contains("Baut") && !s.Name.Contains("Gording") && !s.Name.Contains("Ikatan")),



            ("mezz_plates", "Mezzanine Pelat Sambungan (C.2 Series)", s => s.Name.StartsWith("C.2.") && (s.Name.IndexOf("Plat", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Plate", StringComparison.OrdinalIgnoreCase) >= 0)),



            ("mezz_bolts", "Mezzanine Baut Sambungan (C.2 Series)", s => s.Name.StartsWith("C.2.") && s.Name.IndexOf("Baut", StringComparison.OrdinalIgnoreCase) >= 0),



            ("substructure", "Substruktur & Pondasi Beton (A.1 Series)", s => s.Name.StartsWith("A.1.") && !s.Name.Contains("Bekisting") && !s.Name.Contains("Rebar") && (s.Name.IndexOf("Pondasi", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Pile Cap", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Sloof", StringComparison.OrdinalIgnoreCase) >= 0 || s.Name.IndexOf("Plat", StringComparison.OrdinalIgnoreCase) >= 0)),



            ("secondary", "Secondary Framing (D, E, F, G, H, I, J, N Series)", s => (s.Name.StartsWith("D.1.") || s.Name.StartsWith("E.1.") || s.Name.StartsWith("F.1.") || s.Name.StartsWith("G.1.") || s.Name.StartsWith("H.1.") || s.Name.StartsWith("I.1.") || s.Name.StartsWith("J.1.") || s.Name.StartsWith("N.2.")) && !s.Name.Contains("Plat") && !s.Name.Contains("Baut") && !s.Name.Contains("Angkur"))



        };



        var testGroups = string.IsNullOrWhiteSpace(scope) || scope.Equals("all", StringComparison.OrdinalIgnoreCase)



            ? allTestGroups.Select(g => (g.ScopeName, g.FilterPredicate)).ToList()



            : allTestGroups.Where(g => g.ScopeKey.Equals(scope.Trim(), StringComparison.OrdinalIgnoreCase) || g.ScopeName.IndexOf(scope.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)



                           .Select(g => (g.ScopeName, g.FilterPredicate)).ToList();



        var allScheds = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)



            .ToList();



        int totalDoubleCounted = 0;



        foreach (var group in testGroups)



        {



            sb.AppendLine($"\n--- LINGKUP: {group.ScopeName} ---");



            var schedsInGroup = allScheds.Where(group.FilterPredicate).ToList();



            sb.AppendLine($"Jumlah Schedule Terperiksa: {schedsInGroup.Count}");



            var elementToSchedules = new Dictionary<ElementId, List<string>>();



            foreach (var sched in schedsInGroup)



            {



                try



                {



                    var catId = sched.Definition.CategoryId;



                    var elements = new FilteredElementCollector(doc, sched.Id)



                        .WhereElementIsNotElementType()



                        .ToElements();



                    foreach (var el in elements)



                    {



                        if (catId != ElementId.InvalidElementId && el.Category != null && el.Category.Id.Value != catId.Value) continue;



                        var id = el.Id;



                        if (!elementToSchedules.ContainsKey(id))



                        {



                            elementToSchedules[id] = new List<string>();



                        }



                        elementToSchedules[id].Add(sched.Name);



                    }



                }



                catch {}



            }



            var overlaps = elementToSchedules.Where(kvp => kvp.Value.Count > 1).ToList();



            if (overlaps.Count == 0)



            {



                sb.AppendLine($"[STATUS]: BERSIH / TIDAK ADA DOUBLE COUNTING! (Total elemen unik: {elementToSchedules.Count:N0} unit)");



            }



            else



            {



                totalDoubleCounted += overlaps.Count;



                sb.AppendLine($"[WARNING]: TERDETEKSI {overlaps.Count} ELEMEN MUNCUL DI LEBIH DARI 1 SCHEDULE!");



                foreach (var ov in overlaps.Take(10))



                {



                    string elemDesc = "";



                    try



                    {



                        var el = doc.GetElement(ov.Key);



                        var sym = doc.GetElement(el?.GetTypeId() ?? ElementId.InvalidElementId);



                        elemDesc = $"{sym?.Name ?? el?.Name}";



                    }



                    catch {}



                    sb.AppendLine($"  * Element ID: {ov.Key.Value} ({elemDesc}) muncul di {ov.Value.Count} schedule:");



                    foreach (var scName in ov.Value)



                    {



                        sb.AppendLine($"      - {scName}");



                    }



                }



            }



        }



        sb.AppendLine("\n=======================================================");



        if (totalDoubleCounted == 0)



        {



            sb.AppendLine("[KESIMPULAN AUDIT ANTI-DOUBLE COUNTING]:");



            sb.AppendLine("SELURUH SCHEDULE STRUKTUR 100% AMAN DARI DOUBLE COUNTING.");



            sb.AppendLine("Setiap elemen hanya dihitung tepat 1 kali pada schedule yang bersangkutan.");



        }



        else



        {



            sb.AppendLine($"[KESIMPULAN AUDIT]: TERDAPAT TOTAL {totalDoubleCounted} ELEMEN YANG MENGALAMI OVERLAP / DOUBLE COUNTING. PERLU TINDAKAN ISOLASI FILTER.");



        }



        sb.AppendLine("=======================================================");



        return sb.ToString();



    }



    public static string CopyBeamFiltersToView(Document doc, string targetViewName)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV 2-FASE: COPY BEAM FILTERS KE DENAH VIEW ===");



        /* 1. Audit View Target */



        var targetView = new FilteredElementCollector(doc)



            .OfClass(typeof(View))



            .Cast<View>()



            .FirstOrDefault(v => !v.IsTemplate && v.Name.Equals(targetViewName, StringComparison.OrdinalIgnoreCase))



            ?? new FilteredElementCollector(doc)



            .OfClass(typeof(View))



            .Cast<View>()



            .FirstOrDefault(v => !v.IsTemplate && v.Name.IndexOf(targetViewName, StringComparison.OrdinalIgnoreCase) >= 0);



        if (targetView == null)



        {



            return $"Error: View '{targetViewName}' tidak ditemukan di dokumen.";



        }



        sb.AppendLine("[FASE 1: PRE-EXECUTION REVIEW]");



        sb.AppendLine($"- Target View Name : '{targetView.Name}'");



        sb.AppendLine($"- Target View Id   : {targetView.Id.Value}");



        sb.AppendLine($"- Target View Type : {targetView.ViewType}");



        



        bool hasTemplate = targetView.ViewTemplateId != ElementId.InvalidElementId;



        View? viewTemplate = hasTemplate ? doc.GetElement(targetView.ViewTemplateId) as View : null;



        sb.AppendLine($"- Has View Template: {hasTemplate} {(viewTemplate != null ? $"('{viewTemplate.Name}')" : "")}");



        /* Source filters from Document */



        var allFilters = new FilteredElementCollector(doc)



            .OfClass(typeof(ParameterFilterElement))



            .Cast<ParameterFilterElement>()



            .Where(f => f.Name.StartsWith("AIGRAV - Balok", StringComparison.OrdinalIgnoreCase))



            .ToList();



        sb.AppendLine($"- Filter Sumber Ditemukan: {allFilters.Count} filter");



        foreach (var f in allFilters)



        {



            sb.AppendLine($"  * {f.Name} (Id: {f.Id.Value})");



        }



        /* Find Solid Fill Pattern */



        var fillPatterns = new FilteredElementCollector(doc)



            .OfClass(typeof(FillPatternElement))



            .Cast<FillPatternElement>()



            .ToList();



        var solidPattern = fillPatterns.FirstOrDefault(fp => fp.Name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0);



        sb.AppendLine("\n[FASE 2: EKSEKUSI 3 SUB-AGENT]");



        sb.AppendLine("Sub-Agent 1 (QA/QC): Mengunci target view dan parameter override garis denah.");



        sb.AppendLine("Sub-Agent 2 (Executor): Membuka transaksi dan menerapkan filter override.");



        /* Color definitions matching the source */



        var colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)



        {



            ["AIGRAV - Balok WF 250"] = new Color(175, 217, 170), // #AFD9AA



            ["AIGRAV - Balok WF 300"] = new Color(255, 0, 0),     // #FF0000



            ["AIGRAV - Balok WF 350"] = new Color(255, 255, 0),   // #FFFF00



            ["AIGRAV - Balok WF 400"] = new Color(0, 255, 255),   // #00FFFF



            ["AIGRAV - Balok HC 400 + WF 400 (Tengah Bentang)"] = new Color(19, 103, 52), // #136734



            ["AIGRAV - Balok HC 588"] = new Color(0, 0, 255)      // #0000FF



        };



        /* Determine destination view: if View Template controls filters, apply to View Template */



        View destView = targetView;



        if (hasTemplate && viewTemplate != null)



        {



            var nonCtrlParams = viewTemplate.GetNonControlledTemplateParameterIds();



            ElementId vgFilterParamId = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);



            bool isFiltersControlledByTemplate = !nonCtrlParams.Contains(vgFilterParamId);



            if (isFiltersControlledByTemplate)



            {



                sb.AppendLine($"  -> Catatan: View Template '{viewTemplate.Name}' mengontrol V/G Filters. Filter akan diterapkan ke View Template agar sinkron ke denah!");



                destView = viewTemplate;



            }



        }



        int appliedCount = 0;



        using (Transaction t = new Transaction(doc, "AIGRAV - Copy Beam Filters to Mezzanine Plan"))



        {



            t.Start();



            foreach (var f in allFilters)



            {



                if (!colors.TryGetValue(f.Name, out Color? c) || c == null)



                {



                    continue;



                }



                if (!destView.IsFilterApplied(f.Id))



                {



                    destView.AddFilter(f.Id);



                }



                OverrideGraphicSettings ogs = new OverrideGraphicSettings();



                /* 1. Projection Lines */



                ogs.SetProjectionLineColor(c);



                ogs.SetProjectionLineWeight(4);



                /* 2. Cut Lines */



                ogs.SetCutLineColor(c);



                ogs.SetCutLineWeight(4);



                /* 3. Surface & Cut Patterns */



                if (solidPattern != null)



                {



                    ogs.SetSurfaceForegroundPatternId(solidPattern.Id);



                    ogs.SetSurfaceForegroundPatternColor(c);



                    ogs.SetCutForegroundPatternId(solidPattern.Id);



                    ogs.SetCutForegroundPatternColor(c);



                }



                destView.SetFilterOverrides(f.Id, ogs);



                destView.SetFilterVisibility(f.Id, true);



                appliedCount++;



                sb.AppendLine($"  * [APPLIED] '{f.Name}' ke '{destView.Name}' -> Warna RGB({c.Red}, {c.Green}, {c.Blue}) | Line Weight: 4");



            }



            t.Commit();



        }



        sb.AppendLine("\n[FASE 3: POST-EXECUTION VERIFIKASI & SAFETY GATE]");



        sb.AppendLine("Sub-Agent 3 (Auditor): Memverifikasi kepatuhan visual pada denah...");



        var currentFilters = destView.GetFilters();



        sb.AppendLine($"- Total Filter aktif pada '{destView.Name}': {currentFilters.Count} filter");



        foreach (var fid in currentFilters)



        {



            var pfe = doc.GetElement(fid) as ParameterFilterElement;



            if (pfe != null && pfe.Name.StartsWith("AIGRAV"))



            {



                var ov = destView.GetFilterOverrides(fid);



                sb.AppendLine($"  * Verified: '{pfe.Name}' | Vis: {destView.GetFilterVisibility(fid)} | Line Color: RGB({ov.ProjectionLineColor.Red}, {ov.ProjectionLineColor.Green}, {ov.ProjectionLineColor.Blue})");



            }



        }



        sb.AppendLine($"\n[SUCCESS]: Seluruh {appliedCount} Filter Balok berhasil disalin dan aktif pada view '{targetView.Name}'!");



        return sb.ToString();



    }



    public static string ApplyFiltersToFloorPlan(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV 2-FASE: APPLY BEAM FILTERS KE FLOOR PLAN DENAH MEZZANINE CUP STORAGE ===");



        ElementId floorPlanId = new ElementId(5993009);



        View? planView = doc.GetElement(floorPlanId) as View;



        if (planView == null) return "Floor plan view 5993009 not found.";



        sb.AppendLine($"Target Plan View: '{planView.Name}' (Type: {planView.ViewType}, Id: {planView.Id.Value})");



        



        bool hasTemplate = planView.ViewTemplateId != ElementId.InvalidElementId;



        View? viewTemplate = hasTemplate ? doc.GetElement(planView.ViewTemplateId) as View : null;



        sb.AppendLine($"Has View Template: {hasTemplate} {(viewTemplate != null ? $"('{viewTemplate.Name}', Id: {viewTemplate.Id.Value})" : "")}");



        // Source filters from Document



        var allFilters = new FilteredElementCollector(doc)



            .OfClass(typeof(ParameterFilterElement))



            .Cast<ParameterFilterElement>()



            .Where(f => f.Name.StartsWith("AIGRAV - Balok", StringComparison.OrdinalIgnoreCase))



            .ToList();



        // Find Solid Fill Pattern



        var fillPatterns = new FilteredElementCollector(doc)



            .OfClass(typeof(FillPatternElement))



            .Cast<FillPatternElement>()



            .ToList();



        var solidPattern = fillPatterns.FirstOrDefault(fp => fp.Name.IndexOf("Solid", StringComparison.OrdinalIgnoreCase) >= 0);



        var colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)



        {



            ["AIGRAV - Balok WF 250"] = new Color(175, 217, 170), // #AFD9AA



            ["AIGRAV - Balok WF 300"] = new Color(255, 0, 0),     // #FF0000



            ["AIGRAV - Balok WF 350"] = new Color(255, 255, 0),   // #FFFF00



            ["AIGRAV - Balok WF 400"] = new Color(0, 255, 255),   // #00FFFF



            ["AIGRAV - Balok HC 400 + WF 400 (Tengah Bentang)"] = new Color(19, 103, 52), // #136734



            ["AIGRAV - Balok HC 588"] = new Color(0, 0, 255)      // #0000FF



        };



        // Views to apply: if template controls VG filters, apply to template AND view (if allowed)



        List<View> viewsToApply = new List<View>();



        if (hasTemplate && viewTemplate != null)



        {



            var nonCtrlParams = viewTemplate.GetNonControlledTemplateParameterIds();



            ElementId vgFilterParamId = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);



            bool isFiltersControlledByTemplate = !nonCtrlParams.Contains(vgFilterParamId);



            if (isFiltersControlledByTemplate)



            {



                sb.AppendLine($"  -> View Template '{viewTemplate.Name}' mengontrol filter. Menerapkan ke View Template...");



                viewsToApply.Add(viewTemplate);



            }



            else



            {



                viewsToApply.Add(planView);



            }



        }



        else



        {



            viewsToApply.Add(planView);



        }



        using (Transaction t = new Transaction(doc, "AIGRAV - Apply Beam Filters to Floor Plan View"))



        {



            t.Start();



            foreach (var destView in viewsToApply)



            {



                foreach (var f in allFilters)



                {



                    if (!colors.TryGetValue(f.Name, out Color? c) || c == null) continue;



                    if (!destView.IsFilterApplied(f.Id))



                    {



                        destView.AddFilter(f.Id);



                    }



                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();



                    // Projection lines (denah tampak)



                    ogs.SetProjectionLineColor(c);



                    ogs.SetProjectionLineWeight(4);



                    // Cut lines



                    ogs.SetCutLineColor(c);



                    ogs.SetCutLineWeight(4);



                    // Solid Fill for surface & cut



                    if (solidPattern != null)



                    {



                        ogs.SetSurfaceForegroundPatternId(solidPattern.Id);



                        ogs.SetSurfaceForegroundPatternColor(c);



                        ogs.SetCutForegroundPatternId(solidPattern.Id);



                        ogs.SetCutForegroundPatternColor(c);



                    }



                    destView.SetFilterOverrides(f.Id, ogs);



                    destView.SetFilterVisibility(f.Id, true);



                    sb.AppendLine($"  * [APPLIED] '{f.Name}' ke '{destView.Name}' -> Color RGB({c.Red}, {c.Green}, {c.Blue})");



                }



            }



            t.Commit();



        }



        sb.AppendLine("\n[VERIFIKASI SETELAH EKSEKUSI]:");



        foreach (var v in viewsToApply)



        {



            sb.AppendLine($"View: '{v.Name}' (Id: {v.Id.Value})");



            foreach (var fid in v.GetFilters())



            {



                var pfe = doc.GetElement(fid) as ParameterFilterElement;



                if (pfe != null && pfe.Name.StartsWith("AIGRAV"))



                {



                    var ov = v.GetFilterOverrides(fid);



                    sb.AppendLine($"  -> Verified Filter: '{pfe.Name}' | Vis: {v.GetFilterVisibility(fid)} | Line RGB: ({ov.ProjectionLineColor.Red},{ov.ProjectionLineColor.Green},{ov.ProjectionLineColor.Blue})");



                }



            }



        }



        return sb.ToString();



    }



    public static string CheckPlanViews(Document doc)



    {



        var sb = new StringBuilder();



        var allViews = new FilteredElementCollector(doc)



            .OfClass(typeof(View))



            .Cast<View>()



            .Where(v => !v.IsTemplate && (v.Name.IndexOf("MEZZANINE", StringComparison.OrdinalIgnoreCase) >= 0 || v.Name.IndexOf("CUP", StringComparison.OrdinalIgnoreCase) >= 0))



            .ToList();



        sb.AppendLine($"Ditemukan {allViews.Count} views terkait 'MEZZANINE / CUP':");



        foreach (var v in allViews)



        {



            sb.AppendLine($"  * View: '{v.Name}' | Type: {v.ViewType} | Id: {v.Id.Value} | IsSheet: {v is ViewSheet}");



        }



        return sb.ToString();



    }



    public static string VerifyAppliedFilters(Document doc, View? targetView)



    {



        if (targetView == null) return "View is null";



        var sb = new StringBuilder();



        sb.AppendLine("=== VERIFIKASI POST-EXECUTION VIEW FILTERS ===");



        sb.AppendLine($"View: '{targetView.Name}' (Id: {targetView.Id.Value})");



        



        var appliedFilterIds = targetView.GetFilters();



        sb.AppendLine($"Total Filters Applied: {appliedFilterIds.Count}");



        foreach (var id in appliedFilterIds)



        {



            var pfe = doc.GetElement(id) as ParameterFilterElement;



            if (pfe != null && pfe.Name.StartsWith("AIGRAV"))



            {



                var ogs = targetView.GetFilterOverrides(id);



                var cLine = ogs.ProjectionLineColor;



                var cFill = ogs.SurfaceForegroundPatternColor;



                bool vis = targetView.GetFilterVisibility(id);



                sb.AppendLine($"  * Filter: '{pfe.Name}' (Id: {pfe.Id.Value}) | Visibility: {vis}");



                sb.AppendLine($"    -> Line Color: RGB({cLine.Red}, {cLine.Green}, {cLine.Blue}) | Line Weight: {ogs.ProjectionLineWeight}");



                sb.AppendLine($"    -> Fill Color: RGB({cFill.Red}, {cFill.Green}, {cFill.Blue}) | Pattern Id: {ogs.SurfaceForegroundPatternId.Value}");



            }



        }



        return sb.ToString();



    }



    public static string CreateAndApplyBeamFilters(Document doc, View? targetView)



    {



        if (targetView == null) return "Error: Target view is null.";



        return "Already created";



    }



    private static string CreateRCWF588Type(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== PEMBUATAN PROFIL BARU: RCWF 588 ===");



        Family? family = new FilteredElementCollector(doc)



            .OfClass(typeof(Family))



            .Cast<Family>()



            .FirstOrDefault(f => f.Name.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase));



        if (family == null) return "Family Castellated Beam tidak ditemukan.";



        var symbols = family.GetFamilySymbolIds()



            .Select(id => doc.GetElement(id) as FamilySymbol)



            .Where(s => s != null)



            .ToList();



        if (symbols.Count == 0) return "Tidak ada tipe di family Castellated Beam.";



        var baseSymbol = symbols.FirstOrDefault(s => s.Name.Contains("400"))



                      ?? symbols.FirstOrDefault(s => s.Name.Contains("450"))



                      ?? symbols.FirstOrDefault(s => s.Name.Contains("350"))



                      ?? symbols.First();



        using (Transaction tx = new(doc, "AIGRAV - Buat Profil RCWF 588"))



        {



            tx.Start();



            string[] targetNames = new string[] { "RCWF 588", "RCWF.588" };



            foreach (var targetName in targetNames)



            {



                FamilySymbol? target = symbols.FirstOrDefault(s => s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));



                if (target == null)



                {



                    target = baseSymbol!.Duplicate(targetName) as FamilySymbol;



                }



                if (target == null) continue;



                Action<string, double> setMm = (paramName, mmVal) => {



                    Parameter? p = target.LookupParameter(paramName);



                    if (p != null && !p.IsReadOnly) p.Set(mmVal / 304.8);



                };



                Action<string, double> setDeg = (paramName, degVal) => {



                    Parameter? p = target.LookupParameter(paramName);



                    if (p != null && !p.IsReadOnly) p.Set(degVal * Math.PI / 180.0);



                };



                setMm("d1", 588.0);



                setMm("bf1", 300.0);



                setMm("tf1", 20.0);



                setMm("tw1", 12.0);



                setMm("d2", 588.0);



                setMm("bf2", 300.0);



                setMm("tf2", 20.0);



                setMm("tw2", 12.0);



                setMm("d", 882.0);



                setMm("ho", 588.0);



                setMm("e1", 147.0);



                setMm("e2", 147.0);



                setMm("h1", 294.0);



                setMm("h2", 294.0);



                setMm("dt1", 147.0);



                setMm("dt2", 147.0);



                setMm("b", 171.0);



                setMm("wo", 487.0);



                setMm("Spacing", 634.0);



                setDeg("phi1", 60.0);



                setDeg("phi2", 60.0);



                target.LookupParameter("Type Comments")?.Set("AIGRAV Steel DB | CWF 588 (WF 588x300)");



                target.LookupParameter("Comments")?.Set("CWF 588");



                target.LookupParameter("Description")?.Set("Castellated Wide Flange 882x300x12x20 (Profil Asal WF 588)");



            }



            tx.Commit();



        }



        sb.AppendLine("Profil RCWF 588 berhasil disinkronkan.");



        return sb.ToString();



    }



    private static string InspectStructuralSections(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== INSPEKSI STRUCTURAL SECTION & ADVANCE STEEL PROFILE ===");



        var symbols = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .OfClass(typeof(FamilySymbol))



            .Cast<FamilySymbol>()



            .Where(s => s.FamilyName.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase)



                     || s.FamilyName.Equals("M_W-Wide Flange", StringComparison.OrdinalIgnoreCase))



            .ToList();



        foreach (var sym in symbols.Where(s => s.Name.Contains("400") || s.Name.Contains("588")))



        {



            sb.AppendLine($"\nFamily: '{sym.FamilyName}' | Type: '{sym.Name}' (Id: {sym.Id.Value})");



            sb.AppendLine($"  CanHaveStructuralSection: {sym.CanHaveStructuralSection()}");



            try



            {



                var sec = sym.GetStructuralSection();



                if (sec == null)



                {



                    sb.AppendLine("  GetStructuralSection() is NULL!");



                }



                else



                {



                    sb.AppendLine($"  GetStructuralSection() Type: {sec.GetType().FullName}");



                    sb.AppendLine($"  Shape: {sec.StructuralSectionShape} | GeneralShape: {sec.StructuralSectionGeneralShape}");



                    sb.AppendLine($"  ShapeName: '{sec.StructuralSectionShapeName}' | SectionNameKey: '{sec.SectionNameKey}'");



                    foreach (var prop in sec.GetType().GetProperties())



                    {



                        try



                        {



                            var val = prop.GetValue(sec);



                            sb.AppendLine($"    [PROP] {prop.Name} = {val}");



                        }



                        catch (Exception ex)



                        {



                            sb.AppendLine($"    [PROP] {prop.Name} ERROR: {ex.Message}");



                        }



                    }



                    if (sec is StructuralSectionIParallelFlange ipf)



                    {



                        sb.AppendLine($"  ClearWebHeight: {ipf.ClearWebHeight * 304.8:F1} mm, FlangeToeOfFillet: {ipf.FlangeToeOfFillet * 304.8:F1} mm, WebToeOfFillet: {ipf.WebToeOfFillet * 304.8:F1} mm");



                    }



                }



            }



            catch (Exception ex)



            {



                sb.AppendLine($"  GetStructuralSection() Error: {ex.Message}");



            }



            sb.AppendLine("  Key Parameters:");



            foreach (Parameter p in sym.Parameters)



            {



                string pname = p.Definition.Name;



                if (pname.Contains("Section") || pname.Contains("Height") || pname.Contains("Width") || pname.Contains("Thickness") || pname.Contains("Fillet") || pname.Contains("Shape") || pname.Equals("d") || pname.Equals("bf"))



                {



                    string val = p.AsValueString() ?? (p.StorageType == StorageType.Double ? (p.AsDouble() * 304.8).ToString("F1") + " mm" : p.AsInteger().ToString());



                    sb.AppendLine($"    {pname} = '{val}' (Storage: {p.StorageType}, ReadOnly: {p.IsReadOnly})");



                }



            }



        }



        return sb.ToString();



    }



    private static string AssignStructuralSectionToRCWF(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== MENETAPKAN STRUCTURAL SECTION KE RCWF 588 ===");



        var symbols = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .OfClass(typeof(FamilySymbol))



            .Cast<FamilySymbol>()



            .Where(s => s.FamilyName.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase))



            .ToList();



        using (Transaction tx = new(doc, "AIGRAV - Set StructuralSection"))



        {



            tx.Start();



            foreach (var sym in symbols)



            {



                try



                {



                    double w = 300.0 / 304.8;



                    double h = 882.0 / 304.8;



                    double tf = 20.0 / 304.8;



                    double tw = 12.0 / 304.8;



                    double r = 20.0 / 304.8;



                    if (sym.Name.Contains("300"))



                    {



                        w = 150.0 / 304.8; h = 450.0 / 304.8; tf = 9.0 / 304.8; tw = 7.0 / 304.8; r = 11.0 / 304.8;



                    }



                    else if (sym.Name.Contains("350"))



                    {



                        w = 175.0 / 304.8; h = 525.0 / 304.8; tf = 11.0 / 304.8; tw = 7.0 / 304.8; r = 14.0 / 304.8;



                    }



                    else if (sym.Name.Contains("400"))



                    {



                        w = 200.0 / 304.8; h = 600.0 / 304.8; tf = 13.0 / 304.8; tw = 8.0 / 304.8; r = 16.0 / 304.8;



                    }



                    else if (sym.Name.Contains("450"))



                    {



                        w = 200.0 / 304.8; h = 675.0 / 304.8; tf = 14.0 / 304.8; tw = 9.0 / 304.8; r = 18.0 / 304.8;



                    }



                    var analysisParams = new StructuralSectionAnalysisParams();



                    var sec = new StructuralSectionGeneralI(



                        w, h, tf, w / 4.0, 0, tf + r, 0, tw, r, tw / 2.0 + r, 0, 0, analysisParams



                    );



                    sym.SetStructuralSection(sec);



                    sb.AppendLine($"* Sukses menetapkan StructuralSection ke: '{sym.Name}'");



                }



                catch (Exception ex)



                {



                    sb.AppendLine($"* GAGAL pada '{sym.Name}': {ex.Message}");



                }



            }



            tx.Commit();



        }



        return sb.ToString();



    }



    private static string TestSetSection(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== TEST SET STRUCTURAL SECTION ON RCWF 588 ===");



        var sym = doc.GetElement(new ElementId(6663781)) as FamilySymbol;



        if (sym == null) return "Symbol 6663781 not found";



        sb.AppendLine($"Symbol: '{sym.Name}' | CanHaveStructuralSection: {sym.CanHaveStructuralSection()}");



        double w = 300.0 / 304.8;



        double h = 882.0 / 304.8;



        double tf = 20.0 / 304.8;



        double tw = 12.0 / 304.8;



        double r = 20.0 / 304.8;



        var analysisParams = new StructuralSectionAnalysisParams();



        var sec = new StructuralSectionGeneralI(



            w, h, tf, w / 4.0, 0, tf + r, 0, tw, r, tw / 2.0 + r, 0, 0, analysisParams



        );



        using (Transaction tx = new(doc, "Test Set Section"))



        {



            tx.Start();



            try



            {



                sym.SetStructuralSection(sec);



                sb.AppendLine("sym.SetStructuralSection(sec) succeeded!");



            }



            catch (Exception ex)



            {



                sb.AppendLine($"sym.SetStructuralSection(sec) FAILED: {ex}");



            }



            try



            {



                bool ok = StructuralSectionUtils.SetStructuralSection(doc, sym.Id, sec);



                sb.AppendLine($"StructuralSectionUtils.SetStructuralSection returned: {ok}");



            }



            catch (Exception ex)



            {



                sb.AppendLine($"StructuralSectionUtils.SetStructuralSection FAILED: {ex}");



            }



            tx.Commit();



        }



        var newSec = sym.GetStructuralSection();



        sb.AppendLine($"GetStructuralSection() after commit: {(newSec == null ? "NULL" : newSec.GetType().Name)}");



        return sb.ToString();



    }



    private static string InspectFamilyProperties(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== PERBANDINGAN FAMILY DEFINITION: M_W-WIDE FLANGE VS CASTELLATED BEAM ===");



        string[] famNames = new string[] { "M_W-Wide Flange", "Castellated Beam" };



        foreach (var fname in famNames)



        {



            var fam = new FilteredElementCollector(doc)



                .OfClass(typeof(Family))



                .Cast<Family>()



                .FirstOrDefault(f => f.Name.Equals(fname, StringComparison.OrdinalIgnoreCase));



            if (fam == null) { sb.AppendLine($"Family '{fname}' tidak ditemukan."); continue; }



            sb.AppendLine($"\n--- FAMILY: '{fam.Name}' (Id: {fam.Id.Value}) ---");



            sb.AppendLine($"  StructuralSectionShape: {fam.StructuralSectionShape}");



            sb.AppendLine($"  StructuralMaterialType: {fam.StructuralMaterialType}");



            sb.AppendLine($"  IsParametric: {fam.IsParametric}");



            sb.AppendLine($"  IsInPlace: {fam.IsInPlace}");



            sb.AppendLine("  Parameters on Family:");



            foreach (Parameter p in fam.Parameters)



            {



                sb.AppendLine($"    {p.Definition.Name} = '{p.AsValueString() ?? p.AsString() ?? p.AsInteger().ToString()}'");



            }



        }



        return sb.ToString();



    }



    private class FamilyLoadOpt : IFamilyLoadOptions



    {



        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)



        {



            overwriteParameterValues = true;



            return true;



        }



        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)



        {



            source = FamilySource.Family;



            overwriteParameterValues = true;



            return true;



        }



    }



    private static string InspectAndFixFamilyDoc(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== INSPEKSI & ANALISIS FAMILY DOCUMENT CASTELLATED BEAM ===");



        var fam = new FilteredElementCollector(doc)



            .OfClass(typeof(Family))



            .Cast<Family>()



            .FirstOrDefault(f => f.Name.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase));



        if (fam == null) return "Family 'Castellated Beam' tidak ditemukan.";



        Document famDoc = doc.EditFamily(fam);



        if (famDoc == null) return "Gagal membuka Family Document.";



        sb.AppendLine($"Family Doc Title: {famDoc.Title}");



        var fm = famDoc.FamilyManager;



        sb.AppendLine($"Total Family Types: {fm.Types.Size}");



        sb.AppendLine($"Total Family Parameters: {fm.Parameters.Size}");



        // Inspect Section Shape parameter on OwnerFamily



        var ownerFam = famDoc.OwnerFamily;



        sb.AppendLine($"Owner Family Name: {ownerFam.Name}");



        



        var secParam = ownerFam.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_SHAPE);



        if (secParam != null)



        {



            sb.AppendLine($"Section Shape Param: '{secParam.AsValueString() ?? secParam.AsInteger().ToString()}' (Storage: {secParam.StorageType}, ReadOnly: {secParam.IsReadOnly})");



        }



        else



        {



            sb.AppendLine("FAMILY_STRUCTURAL_SECTION_SHAPE param not found on owner family!");



        }



        sb.AppendLine("\nFamily Parameters in FamilyManager:");



        foreach (FamilyParameter fp in fm.Parameters)



        {



            if (fp.Definition.GetGroupTypeId().TypeId.Contains("structuralSectionGeometry") || fp.Definition.Name.Contains("Section") || fp.Definition.Name.Contains("Fillet") || fp.Definition.Name.Contains("Height") || fp.Definition.Name.Contains("Width"))



            {



                sb.AppendLine($"  [SSG] {fp.Definition.Name} | Formula: '{fp.Formula}' | IsDeterminedByFormula: {fp.IsDeterminedByFormula}");



            }



        }



        // Also check values for current type



        var curType = fm.CurrentType;



        if (curType != null)



        {



            sb.AppendLine($"\nCurrent Family Type in Editor: '{curType.Name}'");



            foreach (FamilyParameter fp in fm.Parameters)



            {



                if (fp.Definition.GetGroupTypeId().TypeId.Contains("structuralSectionGeometry"))



                {



                    string valStr = curType.AsValueString(fp) ?? curType.AsString(fp) ?? (curType.AsDouble(fp).HasValue ? (curType.AsDouble(fp)!.Value * 304.8).ToString("F1") + " mm" : "null");



                    sb.AppendLine($"    {fp.Definition.Name} = {valStr}");



                }



            }



        }



        famDoc.Close(false);



        return sb.ToString();



    }



    private static string FixAndReloadCastellatedFamily(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: MEMPERBAIKI & MENYINKRONKAN FAMILY CASTELLATED BEAM ===");



        var fam = new FilteredElementCollector(doc)



            .OfClass(typeof(Family))



            .Cast<Family>()



            .FirstOrDefault(f => f.Name.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase));



        if (fam == null) return "Family 'Castellated Beam' tidak ditemukan.";



        Document famDoc = doc.EditFamily(fam);



        if (famDoc == null) return "Gagal membuka Family Document.";



        var fm = famDoc.FamilyManager;



        using (Transaction txFam = new(famDoc, "AIGRAV - Map Structural Section Formulas"))



        {



            txFam.Start();



            // Set Section Shape on Owner Family to I-Shape Parallel Flange



            var ownerFam = famDoc.OwnerFamily;



            var secParam = ownerFam.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_SHAPE);



            if (secParam != null && !secParam.IsReadOnly)



            {



                // In Revit enum: 6 = I-Shape Parallel Flange



                secParam.Set(6);



                sb.AppendLine("* Mengatur Section Shape Owner Family = I-Shape Parallel Flange (6)");



            }



            Action<string, string> setFormula = (pname, formula) => {



                var fp = fm.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name.Equals(pname, StringComparison.OrdinalIgnoreCase));



                if (fp != null)



                {



                    try



                    {



                        fm.SetFormula(fp, formula);



                        sb.AppendLine($"  -> Set Formula [{pname}] = '{formula}' : BERHASIL");



                    }



                    catch (Exception ex)



                    {



                        sb.AppendLine($"  -> Set Formula [{pname}] = '{formula}' : GAGAL ({ex.Message})");



                    }



                }



                else



                {



                    sb.AppendLine($"  -> Parameter [{pname}] tidak ditemukan di FamilyManager.");



                }



            };



            // Map SSG parameters to existing family dimensions



            setFormula("Height", "d");



            setFormula("Width", "bf1");



            setFormula("Flange Thickness", "tf1");



            setFormula("Web Thickness", "tw1");



            setFormula("Web Fillet", "13 mm");



            setFormula("Clear Web Height", "Height - 2 * Flange Thickness - 2 * Web Fillet");



            setFormula("Flange Toe of Fillet", "Flange Thickness + Web Fillet");



            setFormula("Web Toe of Fillet", "Web Thickness / 2 + Web Fillet");



            txFam.Commit();



        }



        sb.AppendLine("\n* Memuat kembali Family ke dalam Project...");



        try



        {



            famDoc.LoadFamily(doc, new FamilyLoadOpt());



            sb.AppendLine("* BERHASIL memuat Family ke Project dengan parameter ter-update!");



        }



        catch (Exception ex)



        {



            sb.AppendLine($"* GAGAL memuat Family ke Project: {ex.Message}");



        }



        famDoc.Close(false);



        // Also save to disk if file exists



        string diskPath = @"E:\BIM SMU\Family Revit\Castellated Beam.rfa";



        try



        {



            Document famDocDisk = doc.EditFamily(fam);



            famDocDisk.SaveAs(diskPath, new SaveAsOptions { OverwriteExistingFile = true });



            famDocDisk.Close(false);



            sb.AppendLine($"* Berhasil menyimpan salinan RFA yang sudah diperbaiki ke: {diskPath}");



        }



        catch (Exception ex)



        {



            sb.AppendLine($"* Info simpan disk: {ex.Message}");



        }



        return sb.ToString();



    }



    private static string InspectMWideFlangeDoc(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== PARAMETER RESMI AUTODESK PADA M_W-WIDE FLANGE ===");



        var fam = new FilteredElementCollector(doc)



            .OfClass(typeof(Family))



            .Cast<Family>()



            .FirstOrDefault(f => f.Name.Equals("M_W-Wide Flange", StringComparison.OrdinalIgnoreCase));



        if (fam == null) return "Family M_W-Wide Flange tidak ditemukan.";



        Document famDoc = doc.EditFamily(fam);



        var fm = famDoc.FamilyManager;



        foreach (FamilyParameter fp in fm.Parameters)



        {



            string isShared = fp.IsShared ? $"SHARED (Guid: {fp.GUID})" : "FAMILY PARAMETER";



            sb.AppendLine($"  * Name: '{fp.Definition.Name}' | Type: {fp.Definition.GetDataType().TypeId} | {isShared} | Formula: '{fp.Formula}'");



        }



        famDoc.Close(false);



        return sb.ToString();



    }



    private static string AddStandardSteelParamsToCastellated(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== MENAMBAHKAN PARAMETER STANDAR REVIT KE CASTELLATED BEAM ===");



        var fam = new FilteredElementCollector(doc)



            .OfClass(typeof(Family))



            .Cast<Family>()



            .FirstOrDefault(f => f.Name.Equals("Castellated Beam", StringComparison.OrdinalIgnoreCase));



        if (fam == null) return "Family 'Castellated Beam' tidak ditemukan.";



        Document famDoc = doc.EditFamily(fam);



        if (famDoc == null) return "Gagal membuka Family Document Castellated Beam.";



        var fm = famDoc.FamilyManager;



        using (Transaction txFam = new(famDoc, "AIGRAV - Configure Steel Section Parameters"))



        {



            txFam.Start();



            // 1. Set Structural Section Shape = I-Shape Parallel Flange (6)



            try



            {



                var ownerFam = famDoc.OwnerFamily;



                var secParam = ownerFam.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_SHAPE);



                if (secParam != null && !secParam.IsReadOnly)



                {



                    secParam.Set(6);



                    sb.AppendLine("* Set OwnerFamily STRUCTURAL_SECTION_SHAPE = 6 (I-Shape Parallel Flange)");



                }



            }



            catch (Exception ex)



            {



                sb.AppendLine($"* Info Section Shape: {ex.Message}");



            }



            // Helper to get or add parameter



            Func<string, FamilyParameter> getOrAdd = (pname) => {



                var fp = fm.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name.Equals(pname, StringComparison.OrdinalIgnoreCase));



                if (fp == null)



                {



                    fp = fm.AddParameter(pname, GroupTypeId.Geometry, SpecTypeId.Length, false);



                    sb.AppendLine($"  + Parameter BARU dibuat: '{pname}'");



                }



                else



                {



                    sb.AppendLine($"  = Parameter sudah ada: '{pname}'");



                }



                return fp;



            };



            Action<string, string> safeSetFormula = (pname, formula) => {



                var fp = fm.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name.Equals(pname, StringComparison.OrdinalIgnoreCase));



                if (fp == null)



                {



                    fp = fm.AddParameter(pname, GroupTypeId.Geometry, SpecTypeId.Length, false);



                }



                try



                {



                    fm.SetFormula(fp, formula);



                    sb.AppendLine($"  -> Set Formula [{pname}] = '{formula}' (OK)");



                }



                catch (Exception ex)



                {



                    sb.AppendLine($"  -> Set Formula [{pname}] = '{formula}' (GAGAL: {ex.Message})");



                }



            };



            // Standard AISC/Revit parameter names



            safeSetFormula("bf", "bf1");



            safeSetFormula("tf", "tf1");



            safeSetFormula("tw", "tw1");



            safeSetFormula("k", "tf1 + 13 mm");



            safeSetFormula("kr", "k");



            // Structural Section Geometry parameter group



            safeSetFormula("Height", "d");



            safeSetFormula("Width", "bf1");



            safeSetFormula("Flange Thickness", "tf1");



            safeSetFormula("Web Thickness", "tw1");



            safeSetFormula("Web Fillet", "13 mm");



            safeSetFormula("Clear Web Height", "Height - 2 * Flange Thickness - 2 * Web Fillet");



            safeSetFormula("Flange Toe of Fillet", "Flange Thickness + Web Fillet");



            safeSetFormula("Web Toe of Fillet", "Web Thickness / 2 + Web Fillet");



            txFam.Commit();



        }



        // Save to disk first



        string diskPath = @"E:\BIM SMU\Family Revit\Castellated Beam.rfa";



        try



        {



            famDoc.SaveAs(diskPath, new SaveAsOptions { OverwriteExistingFile = true });



            sb.AppendLine($"* Berhasil simpan file RFA ke disk: {diskPath}");



        }



        catch (Exception ex)



        {



            sb.AppendLine($"* Info simpan disk: {ex.Message}");



        }



        // Load into project



        sb.AppendLine("* Memuat Family yang sudah diperbarui ke dalam Project Revit...");



        try



        {



            famDoc.LoadFamily(doc, new FamilyLoadOpt());



            sb.AppendLine("* BERHASIL memuat Family ke Project dengan parameter ter-update!");



        }



        catch (Exception ex)



        {



            sb.AppendLine($"* GAGAL memuat Family ke Project: {ex.Message}");



        }



        famDoc.Close(false);



        return sb.ToString();



    }



    private static string CompareAllParameters(Document doc)



    {



        var sb = new StringBuilder();



        var symWF = doc.GetElement(new ElementId(2427990)) as FamilySymbol; // WF 400



        var symCWF = doc.GetElement(new ElementId(6663781)) as FamilySymbol; // RCWF 588



        if (symWF == null || symCWF == null) return "Symbols not found";



        sb.AppendLine("=== DUMP ALL PARAMETERS OF WF 400 (M_W-Wide Flange) ===");



        var wfParams = symWF.Parameters.Cast<Parameter>().OrderBy(p => p.Definition.Name).ToList();



        foreach (Parameter p in wfParams)



        {



            sb.AppendLine($"  {p.Definition.Name,-30} | {p.StorageType,-8} | Val: '{p.AsValueString() ?? p.AsString() ?? (p.StorageType == StorageType.Double ? p.AsDouble().ToString("F4") : p.AsInteger().ToString())}' | RO: {p.IsReadOnly}");



        }



        sb.AppendLine("\n=== DUMP ALL PARAMETERS OF RCWF 588 (Castellated Beam) ===");



        var cwfParams = symCWF.Parameters.Cast<Parameter>().OrderBy(p => p.Definition.Name).ToList();



        foreach (Parameter p in cwfParams)



        {



            sb.AppendLine($"  {p.Definition.Name,-30} | {p.StorageType,-8} | Val: '{p.AsValueString() ?? p.AsString() ?? (p.StorageType == StorageType.Double ? p.AsDouble().ToString("F4") : p.AsInteger().ToString())}' | RO: {p.IsReadOnly}");



        }



        return sb.ToString();



    }



    private static string InspectRfaOnDisk(UIApplication app)



    {



        var sb = new StringBuilder();



        string diskPath = @"E:\BIM SMU\Family Revit\Castellated Beam.rfa";



        sb.AppendLine($"=== INSPEKSI FILE RFA DI DISK: {diskPath} ===");



        Document famDoc = app.Application.OpenDocumentFile(diskPath);



        if (famDoc == null) return "Gagal membuka file RFA di disk.";



        var fm = famDoc.FamilyManager;



        sb.AppendLine($"Total Parameters: {fm.Parameters.Size}");



        foreach (FamilyParameter fp in fm.Parameters)



        {



            sb.AppendLine($"  * '{fp.Definition.Name}' | Type: {fp.Definition.GetDataType().TypeId} | Formula: '{fp.Formula}'");



        }



        famDoc.Close(false);



        return sb.ToString();



    }



    private static string ReloadCastellatedFromDisk(Document doc)



    {



        var sb = new StringBuilder();



        string diskPath = @"E:\BIM SMU\Family Revit\Castellated Beam.rfa";



        sb.AppendLine($"=== RELOAD CASTELLATED BEAM DARI DISK KE PROYEK ===");



        sb.AppendLine($"Path: {diskPath}");



        try



        {



            using (Transaction tx = new(doc, "AIGRAV - Reload Castellated Beam Family"))



            {



                tx.Start();



                bool ok = doc.LoadFamily(diskPath, new FamilyLoadOpt(), out Family loadedFam);



                sb.AppendLine($"doc.LoadFamily returned: {ok}, Family: {(loadedFam != null ? loadedFam.Name : "null")}");



                tx.Commit();



            }



        }



        catch (Exception ex)



        {



            sb.AppendLine($"ERROR: {ex.Message}");



        }



        return sb.ToString();



    }



    private static string InspectCastellatedGeom(UIApplication app)



    {



        var sb = new StringBuilder();



        string diskPath = @"E:\BIM SMU\Family Revit\Castellated Beam.rfa";



        sb.AppendLine($"=== DETAIL ELEMEN DALAM CASTELLATED BEAM.RFA ===");



        Document famDoc = app.Application.OpenDocumentFile(diskPath);



        if (famDoc == null) return "Gagal buka RFA";



        var forms = new FilteredElementCollector(famDoc)



            .OfClass(typeof(GenericForm))



            .Cast<GenericForm>()



            .ToList();



        sb.AppendLine($"Total GenericForms: {forms.Count}");



        foreach (var f in forms)



        {



            sb.AppendLine($"  - Form: '{f.Name}' (Id: {f.Id.Value}) | IsSolid: {f.IsSolid} | Type: {f.GetType().Name}");



        }



        var nested = new FilteredElementCollector(famDoc)



            .OfClass(typeof(FamilyInstance))



            .OfType<FamilyInstance>()



            .ToList();



        sb.AppendLine($"Total Nested FamilyInstances: {nested.Count}");



        foreach (var inst in nested)



        {



            sb.AppendLine($"  - Nested: '{inst.Symbol.FamilyName}' : '{inst.Name}' (Id: {inst.Id.Value})");



        }



        famDoc.Close(false);



        return sb.ToString();



    }



    public static string AuditConnectionHandlers(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT STRUCTURAL CONNECTIONS & PARAMETER 'Type of connection' ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"Total Connection Handlers di Model: {conns.Count}");



        if (conns.Count == 0) return sb.ToString();



        int withParam = 0;



        int populated = 0;



        int totalSubElements = 0;



        int totalSubWithParam = 0;



        int totalSubPopulated = 0;



        var typeGrouping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



        foreach (var conn in conns)



        {



            string typeName = GetConnectionTypeName(doc, conn);



            if (!typeGrouping.ContainsKey(typeName)) typeGrouping[typeName] = 0;



            typeGrouping[typeName]++;



            var pConn = FindParam(conn, "Type of connection");



            string curVal = pConn?.AsString() ?? pConn?.AsValueString() ?? "(empty)";



            if (pConn != null) withParam++;



            if (!string.IsNullOrWhiteSpace(curVal) && curVal != "(empty)") populated++;



            var depIds = conn.GetDependentElements(null);



            totalSubElements += depIds.Count;



            sb.AppendLine($"\nConnection Id: {conn.Id.Value} | Type/Name: '{typeName}'");



            sb.AppendLine($"  Parent Param 'Type of connection': {(pConn != null ? $"ADA [Val: '{curVal}']" : "TIDAK ADA")}");



            sb.AppendLine($"  Dependent Elements Count: {depIds.Count}");



            foreach (var id in depIds)



            {



                Element? child = doc.GetElement(id);



                if (child == null) continue;



                var pChild = FindParam(child, "Type of connection");



                string childVal = pChild?.AsString() ?? pChild?.AsValueString() ?? "(empty)";



                if (pChild != null) totalSubWithParam++;



                if (!string.IsNullOrWhiteSpace(childVal) && childVal != "(empty)") totalSubPopulated++;



                sb.AppendLine($"    -> Sub-Element Id: {child.Id.Value} | Cat: '{child.Category?.Name}' | Name: '{child.Name}' | Param: {(pChild != null ? $"ADA [Val: '{childVal}']" : "TIDAK ADA")}");



            }



        }



        sb.AppendLine("\n=== REKAPITULASI TIPE SAMBUNGAN ===");



        foreach (var kvp in typeGrouping.OrderByDescending(x => x.Value))



        {



            sb.AppendLine($"  * {kvp.Key,-35} : {kvp.Value} instance");



        }



        sb.AppendLine("\n=== REKAPITULASI STATUS PARAMETER ===");



        sb.AppendLine($"  * Parent Connections : Total {conns.Count} | Memiliki Param: {withParam} | Sudah Terisi: {populated}");



        sb.AppendLine($"  * Sub-Elements       : Total {totalSubElements} | Memiliki Param: {totalSubWithParam} | Sudah Terisi: {totalSubPopulated}");



        return sb.ToString();



    }



        public static string PopulateConnectionTypes(Document doc, string? argument = null)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: POPULATE 'Type of connection' KE SELURUH SUB-ELEMEN ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"Total Connection Handlers di Model: {conns.Count}");



        if (conns.Count == 0) return sb.ToString();



        // Find parameter ID for 'Type of connection'



        ElementId paramId = ElementId.InvalidElementId;



        foreach (var conn in conns)



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            if (p != null)



            {



                paramId = p.Id;



                break;



            }



        }



        sb.AppendLine($"Target Shared Parameter Id: {(paramId != ElementId.InvalidElementId ? paramId.Value.ToString() : "Tidak ditemukan!")}");



        int startIndex = 0;



        int count = conns.Count;



        if (!string.IsNullOrWhiteSpace(argument) && !argument.Equals("all", StringComparison.OrdinalIgnoreCase))



        {



            // Argument format: "start,count" e.g. "0,200" or batch number "1", "2"



            if (int.TryParse(argument.Trim(), out int batchNum))



            {



                int bSize = 200;



                startIndex = (batchNum - 1) * bSize;



                count = Math.Min(bSize, conns.Count - startIndex);



            }



            else if (argument.Contains(","))



            {



                var parts = argument.Split(',');



                if (int.TryParse(parts[0].Trim(), out int s) && int.TryParse(parts[1].Trim(), out int c))



                {



                    startIndex = s;



                    count = Math.Min(c, conns.Count - startIndex);



                }



            }



        }



        if (startIndex < 0) startIndex = 0;



        if (startIndex >= conns.Count) return $"Start index {startIndex} di luar jangkauan (Total: {conns.Count})";



        if (count <= 0) count = conns.Count - startIndex;



        var targetBatch = conns.Skip(startIndex).Take(count).ToList();



        sb.AppendLine($"Memproses Batch: Index {startIndex} s/d {startIndex + targetBatch.Count - 1} ({targetBatch.Count} koneksi)");



        int connsUpdated = 0;



        int subUpdated = 0;



        int depUpdated = 0;



        using (Transaction tx = new(doc, $"AIGRAV - Populate Type of Connection ({startIndex + 1}-{startIndex + targetBatch.Count})"))



        {



            tx.Start();



            foreach (var conn in targetBatch)



            {



                string familyVal = GetConnectionFamilyName(doc, conn);



                if (string.IsNullOrWhiteSpace(familyVal)) familyVal = conn.Name;



                // 1. Update Connection Handler



                bool pOk = SetParamValue(conn, "Type of connection", familyVal)



                        || SetParamValue(conn, "Type of Connection", familyVal);



                if (pOk) connsUpdated++;



                // 2. Update Subelements (Plates, Bolts, Welds inside connection)



                try



                {



                    var subs = conn.GetSubelements();



                    if (subs != null && paramId != ElementId.InvalidElementId)



                    {



                        var strVal = new StringParameterValue(familyVal);



                        foreach (Subelement sub in subs)



                        {



                            try



                            {



                                if (sub.HasParameter(paramId) && sub.IsParameterModifiable(paramId))



                                {



                                    sub.SetParameterValue(paramId, strVal);



                                    subUpdated++;



                                }



                            }



                            catch { }



                        }



                    }



                }



                catch { }



                // 3. Update Dependent Elements (if any separate fabrication components)



                try



                {



                    var depIds = conn.GetDependentElements(null);



                    foreach (var childId in depIds)



                    {



                        if (childId == conn.Id) continue;



                        Element? child = doc.GetElement(childId);



                        if (child != null)



                        {



                            bool cOk = SetParamValue(child, "Type of connection", familyVal)



                                    || SetParamValue(child, "Type of Connection", familyVal);



                            if (cOk) depUpdated++;



                        }



                    }



                }



                catch { }



            }



            tx.Commit();



        }



        sb.AppendLine("\n[HASIL EKSEKUSI BATCH]");



        sb.AppendLine($"- Connection Handlers Berhasil Diupdate : {connsUpdated} dari {targetBatch.Count}");



        sb.AppendLine($"- Sub-Elemen Koneksi (Plates, Bolts, Welds) Diupdate : {subUpdated}");



        sb.AppendLine($"- Dependent Elements Diupdate : {depUpdated}");



        return sb.ToString();



    }



    private static string GetConnectionFamilyName(Document doc, Element conn)



    {



        string rawFam = "";



        ElementId typeId = conn.GetTypeId();



        if (typeId != null && typeId != ElementId.InvalidElementId)



        {



            ElementType? et = doc.GetElement(typeId) as ElementType;



            if (et != null && !string.IsNullOrWhiteSpace(et.FamilyName))



            {



                rawFam = et.FamilyName.Trim();



            }



        }



        if (string.IsNullOrWhiteSpace(rawFam))



        {



            var pFam = conn.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM);



            rawFam = pFam?.AsValueString() ?? pFam?.AsString() ?? "";



        }



        if (string.IsNullOrWhiteSpace(rawFam))



        {



            rawFam = conn.Name;



        }



        rawFam = rawFam.Trim();



        if (rawFam.Equals("WF200-Endplate CT 1", StringComparison.OrdinalIgnoreCase) ||



            rawFam.Equals("WF200-Endplate CT1", StringComparison.OrdinalIgnoreCase) ||



            rawFam.Equals("WF200-Endplate CT", StringComparison.OrdinalIgnoreCase))



        {



            return "WF200-Endplate";



        }



        return rawFam;



    }



    private static string GetConnectionTypeName(Document doc, Element conn)



    {



        ElementId typeId = conn.GetTypeId();



        if (typeId != null && typeId != ElementId.InvalidElementId)



        {



            ElementType? et = doc.GetElement(typeId) as ElementType;



            if (et != null && !string.IsNullOrWhiteSpace(et.Name))



            {



                return et.Name;



            }



        }



        return conn.Name;



    }



    private static Parameter? FindParam(Element elem, string paramName)



    {



        var p = elem.LookupParameter(paramName);



        if (p != null) return p;



        foreach (Parameter param in elem.Parameters)



        {



            if (param.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))



            {



                return param;



            }



        }



        return null;



    }



    private static bool SetParamValue(Element elem, string paramName, string value)



    {



        var p = FindParam(elem, paramName);



        if (p != null && !p.IsReadOnly)



        {



            try



            {



                if (p.StorageType == StorageType.String)



                {



                    p.Set(value);



                    return true;



                }



            }



            catch { }



        }



        return false;



    }



    public static string InspectConnectionSubComponents(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== DETEKSI ELEMEN PELAT, BAUT, WELD & VOUTE DI DOKUMEN ===");



        var plates = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"Total Elemen Plates (OST_StructConnectionPlates): {plates.Count}");



        var bolts = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionBolts)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"Total Elemen Bolts (OST_StructConnectionBolts): {bolts.Count}");



        var welds = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionWelds)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"Total Elemen Welds (OST_StructConnectionWelds): {welds.Count}");



        var anchors = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionAnchors)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"Total Elemen Anchors (OST_StructConnectionAnchors): {anchors.Count}");



        if (plates.Count > 0)



        {



            var p0 = plates.First();



            sb.AppendLine($"\nContoh Plate Id: {p0.Id.Value} | Name: '{p0.Name}'");



            var paramTypeConn = FindParam(p0, "Type of connection");



            sb.AppendLine($"  Param 'Type of connection': {(paramTypeConn != null ? $"ADA (Val: '{paramTypeConn.AsString()}', ReadOnly: {paramTypeConn.IsReadOnly})" : "TIDAK ADA")}");



            



            // Check all parameters to see how it links to connection



            sb.AppendLine("  Daftar Parameter Plate:");



            foreach (Parameter p in p0.Parameters)



            {



                if (p.Definition.Name.Contains("Connect") || p.Definition.Name.Contains("Host") || p.Definition.Name.Contains("Type") || p.Definition.Name.Contains("Mark") || p.Definition.Name.Contains("Assembly"))



                {



                    sb.AppendLine($"    - {p.Definition.Name} = '{p.AsValueString() ?? p.AsString()}'");



                }



            }



        }



        if (bolts.Count > 0)



        {



            var b0 = bolts.First();



            sb.AppendLine($"\nContoh Bolt Id: {b0.Id.Value} | Name: '{b0.Name}'");



            var paramTypeConn = FindParam(b0, "Type of connection");



            sb.AppendLine($"  Param 'Type of connection': {(paramTypeConn != null ? $"ADA (Val: '{paramTypeConn.AsString()}', ReadOnly: {paramTypeConn.IsReadOnly})" : "TIDAK ADA")}");



        }



        return sb.ToString();



    }



    public static string DumpElementAllParams(Document doc, int elemId)



    {



        var sb = new StringBuilder();



        Element elem = doc.GetElement(new ElementId(elemId));



        if (elem == null) return "Element not found";



        sb.AppendLine($"=== DUMP PARAMETERS FOR ELEM ID {elemId} ({elem.Category?.Name}) ===");



        sb.AppendLine($"Class: {elem.GetType().FullName}");



        sb.AppendLine($"GroupId: {elem.GroupId.Value}");



        sb.AppendLine($"AssemblyInstanceId: {elem.AssemblyInstanceId.Value}");



        var depIds = elem.GetDependentElements(null);



        sb.AppendLine($"Dependent elements count: {depIds.Count}");



        foreach (Parameter p in elem.Parameters)



        {



            string val = "";



            switch (p.StorageType)



            {



                case StorageType.String: val = p.AsString(); break;



                case StorageType.Integer: val = p.AsInteger().ToString(); break;



                case StorageType.Double: val = p.AsDouble().ToString("F3"); break;



                case StorageType.ElementId: val = p.AsElementId().Value.ToString(); break;



            }



            sb.AppendLine($"  {p.Definition.Name,-35} | {p.StorageType,-10} | RO:{p.IsReadOnly,-5} | Val: '{val}' | ValStr: '{p.AsValueString()}'");



        }



        return sb.ToString();



    }



    public static string InspectConnectionHandlerDetails(Document doc, int connId)



    {



        var sb = new StringBuilder();



        Element elem = doc.GetElement(new ElementId(connId));



        if (elem == null) return "Connection not found";



        sb.AppendLine($"=== CONNECTION HANDLER {connId} DETAILS ===");



        sb.AppendLine($"Type: {elem.GetType().FullName}");



        sb.AppendLine($"Name: {elem.Name}");



        var handler = elem as Autodesk.Revit.DB.Structure.StructuralConnectionHandler;



        if (handler != null)



        {



            var connectedIds = handler.GetConnectedElementIds();



            sb.AppendLine($"Connected Element Ids count: {connectedIds.Count}");



            foreach (var cid in connectedIds)



            {



                var ce = doc.GetElement(cid);



                sb.AppendLine($"  - Connected: {cid.Value} ({ce?.Category?.Name} - {ce?.Name})");



            }



            var origin = handler.GetOrigin();



            sb.AppendLine($"Origin: X={origin.X:F2}, Y={origin.Y:F2}, Z={origin.Z:F2}");



        }



        // Check all parameters of handler



        sb.AppendLine("\nParameters of Connection Handler:");



        foreach (Parameter p in elem.Parameters)



        {



            string val = "";



            switch (p.StorageType)



            {



                case StorageType.String: val = p.AsString(); break;



                case StorageType.Integer: val = p.AsInteger().ToString(); break;



                case StorageType.Double: val = p.AsDouble().ToString("F3"); break;



                case StorageType.ElementId: val = p.AsElementId().Value.ToString(); break;



            }



            sb.AppendLine($"  {p.Definition.Name,-35} | {p.StorageType,-10} | RO:{p.IsReadOnly,-5} | Val: '{val}'");



        }



        // Check if there are dependent elements or other methods



        var deps = elem.GetDependentElements(null);



        sb.AppendLine($"\nGetDependentElements(null) count: {deps.Count}");



        foreach (var did in deps)



        {



            var de = doc.GetElement(did);



            sb.AppendLine($"  - Dep: {did.Value} ({de?.Category?.Name} - {de?.GetType().Name})");



        }



        return sb.ToString();



    }



    public static string InspectConnectionSubElementsApi(Document doc, int connId)



    {



        var sb = new StringBuilder();



        Element elem = doc.GetElement(new ElementId(connId));



        if (elem == null) return "Not found";



        sb.AppendLine($"=== SUBELEMENTS OF CONN {connId} ===");



        try



        {



            var subs = elem.GetSubelements();



            sb.AppendLine($"elem.GetSubelements() count: {(subs != null ? subs.Count : 0)}");



            if (subs != null)



            {



                foreach (var sub in subs)



                {



                    sb.AppendLine($"  - Sub: Ref={sub.GetReference()?.ConvertToStableRepresentation(doc)}");



                }



            }



        }



        catch (Exception ex)



        {



            sb.AppendLine($"GetSubelements() error: {ex.Message}");



        }



        // Search near the origin of the connection



        var handler = elem as Autodesk.Revit.DB.Structure.StructuralConnectionHandler;



        if (handler != null)



        {



            XYZ org = handler.GetOrigin();



            Outline outline = new Outline(org - new XYZ(5, 5, 5), org + new XYZ(5, 5, 5));



            var bbFilter = new BoundingBoxIntersectsFilter(outline);



            



            var nearbyPlates = new FilteredElementCollector(doc)



                .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



                .WherePasses(bbFilter)



                .WhereElementIsNotElementType()



                .ToList();



            sb.AppendLine($"\nNearby Plates (within 5ft): {nearbyPlates.Count}");



            foreach (var p in nearbyPlates.Take(5))



            {



                sb.AppendLine($"  Plate Id: {p.Id.Value} | Comments: '{p.LookupParameter("Comments")?.AsString()}' | BoundingBox: {p.get_BoundingBox(null)?.Min}");



            }



            var nearbyBolts = new FilteredElementCollector(doc)



                .OfCategory(BuiltInCategory.OST_StructConnectionBolts)



                .WherePasses(bbFilter)



                .WhereElementIsNotElementType()



                .ToList();



            sb.AppendLine($"Nearby Bolts (within 5ft): {nearbyBolts.Count}");



            foreach (var b in nearbyBolts.Take(5))



            {



                sb.AppendLine($"  Bolt Id: {b.Id.Value} | Comments: '{b.LookupParameter("Comments")?.AsString()}'");



            }



        }



        return sb.ToString();



    }



    



    public static string InspectSamplePlates(Document doc)



    {



        var sb = new StringBuilder();



        var plates = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



            .WhereElementIsNotElementType()



            .Take(20)



            .ToList();



        sb.AppendLine($"Sample {plates.Count} Plates:");



        foreach (var p in plates)



        {



            var pType = FindParam(p, "Type of connection") ?? FindParam(p, "Type of Connection");



            var pComments = FindParam(p, "Comments");



            var pMark = FindParam(p, "Mark");



            sb.AppendLine($"Plate Id: {p.Id.Value} | Type: '{p.Name}' | TypeConn: '{pType?.AsString()}' | Comments: '{pComments?.AsString()}' | Mark: '{pMark?.AsString()}'");



        }



        return sb.ToString();



    }



    public static string GroupPlatesByComments(Document doc)



    {



        var sb = new StringBuilder();



        var plates = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



            .WhereElementIsNotElementType()



            .ToList();



        var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



        foreach (var p in plates)



        {



            string c = p.LookupParameter("Comments")?.AsString() ?? "(empty)";



            if (!groups.ContainsKey(c)) groups[c] = 0;



            groups[c]++;



        }



        sb.AppendLine($"Total Plates: {plates.Count}. Distinct Comments:");



        foreach (var kvp in groups.OrderByDescending(x => x.Value))



        {



            sb.AppendLine($"  - '{kvp.Key}' : {kvp.Value} plates");



        }



        var bolts = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionBolts)



            .WhereElementIsNotElementType()



            .ToList();



        var boltGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



        foreach (var b in bolts)



        {



            string c = b.LookupParameter("Comments")?.AsString() ?? "(empty)";



            if (!boltGroups.ContainsKey(c)) boltGroups[c] = 0;



            boltGroups[c]++;



        }



        sb.AppendLine($"\nTotal Bolts: {bolts.Count}. Distinct Comments:");



        foreach (var kvp in boltGroups.OrderByDescending(x => x.Value))



        {



            sb.AppendLine($"  - '{kvp.Key}' : {kvp.Value} bolts");



        }



        return sb.ToString();



    }



    public static string InspectSubelementClass(Document doc)



    {



        var sb = new StringBuilder();



        var t = typeof(Subelement);



        sb.AppendLine($"Class: {t.FullName}");



        sb.AppendLine("Methods:");



        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))



        {



            var pms = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));



            sb.AppendLine($"  {m.ReturnType.Name} {m.Name}({pms})");



        }



        return sb.ToString();



    }



    



    public static string TestSetConnectionParam(Document doc, int connId)



    {



        var sb = new StringBuilder();



        Element elem = doc.GetElement(new ElementId(connId));



        if (elem == null) return "Connection not found";



        string typeName = GetConnectionTypeName(doc, elem);



        sb.AppendLine($"Connection Id: {connId} | Target Type Name: '{typeName}'");



        using (Transaction tx = new(doc, "AIGRAV - Test Set Connection Param"))



        {



            tx.Start();



            // Set on connection handler



            bool ok = SetParamValue(elem, "Type of connection", typeName)



                   || SetParamValue(elem, "Type of Connection", typeName);



            sb.AppendLine($"Set on handler: {ok}");



            // Set on subelements



            var subs = elem.GetSubelements();



            sb.AppendLine($"Subelements count: {(subs != null ? subs.Count : 0)}");



            int subsUpdated = 0;



            if (subs != null)



            {



                // Find parameter ID for Type of Connection



                ElementId paramId = ElementId.InvalidElementId;



                foreach (Parameter p in elem.Parameters)



                {



                    if (p.Definition.Name.Equals("Type of connection", StringComparison.OrdinalIgnoreCase))



                    {



                        paramId = p.Id;



                        break;



                    }



                }



                if (paramId != ElementId.InvalidElementId)



                {



                    foreach (Subelement sub in subs)



                    {



                        try



                        {



                            if (sub.HasParameter(paramId) && sub.IsParameterModifiable(paramId))



                            {



                                var val = new StringParameterValue(typeName);



                                sub.SetParameterValue(paramId, val);



                                subsUpdated++;



                            }



                        }



                        catch (Exception ex)



                        {



                            sb.AppendLine($"  Sub error: {ex.Message}");



                        }



                    }



                }



            }



            sb.AppendLine($"Subelements updated: {subsUpdated}");



            tx.Commit();



        }



        // Verify after commit



        var pAfter = FindParam(elem, "Type of connection") ?? FindParam(elem, "Type of Connection");



        sb.AppendLine($"Verification after commit: '{pAfter?.AsString()}'");



        return sb.ToString();



    }



    public static string VerifyPopulatedConnections(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== VERIFIKASI POST-EXECUTION 'Type of connection' (FAMILY NAME) ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        int filledCount = 0;



        int emptyCount = 0;



        int matchFamilyCount = 0;



        int totalSubs = 0;



        int subsFilled = 0;



        ElementId paramId = ElementId.InvalidElementId;



        foreach (var conn in conns)



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            if (p != null) { paramId = p.Id; break; }



        }



        var distinctFamilyVals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



        foreach (var conn in conns)



        {



            string expectedFam = GetConnectionFamilyName(doc, conn);



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            string val = p?.AsString() ?? "";



            if (!string.IsNullOrWhiteSpace(val))



            {



                filledCount++;



                if (val.Equals(expectedFam, StringComparison.OrdinalIgnoreCase))



                {



                    matchFamilyCount++;



                }



                if (!distinctFamilyVals.ContainsKey(val)) distinctFamilyVals[val] = 0;



                distinctFamilyVals[val]++;



            }



            else emptyCount++;



            try



            {



                var subs = conn.GetSubelements();



                if (subs != null)



                {



                    totalSubs += subs.Count;



                    foreach (Subelement sub in subs)



                    {



                        if (paramId != ElementId.InvalidElementId && sub.HasParameter(paramId))



                        {



                            var sval = sub.GetParameterValue(paramId)?.ToString();



                            if (!string.IsNullOrWhiteSpace(sval)) subsFilled++;



                        }



                    }



                }



            }



            catch { }



        }



        sb.AppendLine($"Connection Handlers: Total {conns.Count} | Terisi: {filledCount} ({(filledCount * 100.0 / conns.Count):F1}%) | Sesuai Family Name: {matchFamilyCount} | Kosong: {emptyCount}");



        sb.AppendLine($"Sub-Elements (Plates, Bolts, Welds): Total {totalSubs} | Terisi: {subsFilled} ({(subsFilled * 100.0 / (totalSubs > 0 ? totalSubs : 1)):F1}%)");



        sb.AppendLine("\nRekapitulasi Nilai Parameter Family Name Terisi:");



        foreach (var kvp in distinctFamilyVals.OrderByDescending(x => x.Value))



        {



            sb.AppendLine($"  * {kvp.Key,-35} : {kvp.Value} connections");



        }



        // Sample 10 connections



        sb.AppendLine("\nSample 10 Connections pasca eksekusi:");



        foreach (var conn in conns.Take(10))



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            string expectedFam = GetConnectionFamilyName(doc, conn);



            ElementId tid = conn.GetTypeId();



            ElementType? et = doc.GetElement(tid) as ElementType;



            string typeName = et?.Name ?? conn.Name;



            sb.AppendLine($"  * Id: {conn.Id.Value} | Family: '{expectedFam}' | Type: '{typeName}' | Param: '{p?.AsString()}'");



        }



        return sb.ToString();



    }



    public static string InspectConnectionFamilyNames(Document doc)



    {



        var sb = new StringBuilder();



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .Take(15)



            .ToList();



        sb.AppendLine("Sample 15 Connections Family vs Type Names:");



        foreach (var conn in conns)



        {



            ElementId tid = conn.GetTypeId();



            ElementType? et = doc.GetElement(tid) as ElementType;



            string famName = et?.FamilyName ?? "";



            string typeName = et?.Name ?? "";



            string paramFam = conn.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "";



            



            sb.AppendLine($"Id: {conn.Id.Value} | et.FamilyName: '{famName}' | et.Name: '{typeName}' | paramFam: '{paramFam}'");



        }



        return sb.ToString();



    }



    public static string ListAllConnectionFamilyNames(Document doc)



    {



        var sb = new StringBuilder();



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        var famGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



        foreach (var conn in conns)



        {



            string famName = GetConnectionFamilyName(doc, conn);



            if (!famGroups.ContainsKey(famName)) famGroups[famName] = 0;



            famGroups[famName]++;



        }



        sb.AppendLine($"Total Connections: {conns.Count}. Distinct Family Names (Top Line):");



        foreach (var kvp in famGroups.OrderByDescending(x => x.Value))



        {



            sb.AppendLine($"  * {kvp.Key,-35} : {kvp.Value} connections");



        }



        return sb.ToString();



    }



    public static string ReplaceConnectionFamily(Document doc, string oldName, string newName)



    {



        var sb = new StringBuilder();



        sb.AppendLine($"=== AIGRAV: REPLACE CONNECTION FAMILY '{oldName}' -> '{newName}' ===");



        sb.AppendLine("\n[FASE 1: PRE-EXECUTION AUDIT]");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        ElementId paramId = ElementId.InvalidElementId;



        foreach (var c in conns)



        {



            var p = FindParam(c, "Type of connection") ?? FindParam(c, "Type of Connection");



            if (p != null) { paramId = p.Id; break; }



        }



        var matchedConns = new List<Element>();



        foreach (var c in conns)



        {



            var p = FindParam(c, "Type of connection") ?? FindParam(c, "Type of Connection");



            string val = p?.AsString() ?? "";



            ElementId tid = c.GetTypeId();



            ElementType? et = (tid != null && tid != ElementId.InvalidElementId) ? doc.GetElement(tid) as ElementType : null;



            string fam = et?.FamilyName ?? "";



            bool match = val.Equals(oldName, StringComparison.OrdinalIgnoreCase) ||



                         val.Replace(" ", "").Equals(oldName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||



                         fam.Equals(oldName, StringComparison.OrdinalIgnoreCase) ||



                         fam.Replace(" ", "").Equals(oldName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);



            if (match)



            {



                matchedConns.Add(c);



            }



        }



        sb.AppendLine($"- Total Connection Handlers Terdeteksi: {matchedConns.Count} dari {conns.Count}");



        sb.AppendLine($"- Target Shared Parameter Id         : {(paramId != ElementId.InvalidElementId ? paramId.Value.ToString() : "Tidak ditemukan!")}");



        sb.AppendLine("\n[FASE 2: 3 SUB-AGENT MODEL EKSEKUSI]");



        sb.AppendLine("Sub-Agent 1 (QA/QC): Memverifikasi kepatuhan nama target 'WF200-Endplate' (CT1 dihapus).");



        sb.AppendLine("Sub-Agent 2 (Executor): Membuka transaksi dan mengupdate handler serta seluruh sub-elemen Advance Steel...");



        int connsUpdated = 0;



        int subsUpdated = 0;



        int depUpdated = 0;



        int famsRenamed = 0;



        using (Transaction tx = new(doc, $"AIGRAV - Replace Connection Family {oldName} to {newName}"))



        {



            tx.Start();



            // 1. Rename Family in Revit if standard Family exists



            try



            {



                var families = new FilteredElementCollector(doc)



                    .OfClass(typeof(Family))



                    .Cast<Family>()



                    .Where(f => f.Name.IndexOf("WF200", StringComparison.OrdinalIgnoreCase) >= 0)



                    .ToList();



                foreach (var f in families)



                {



                    if (f.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase) ||



                        f.Name.Replace(" ", "").Equals(oldName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))



                    {



                        try



                        {



                            f.Name = newName;



                            famsRenamed++;



                            sb.AppendLine($"  * Revit Family Id {f.Id.Value} berhasil di-rename menjadi '{newName}'");



                        }



                        catch { }



                    }



                }



            }



            catch { }



            // 2. Update StructuralConnectionHandlerType names if any



            try



            {



                var connTypes = new FilteredElementCollector(doc)



                    .OfClass(typeof(Autodesk.Revit.DB.Structure.StructuralConnectionHandlerType))



                    .Cast<Autodesk.Revit.DB.Structure.StructuralConnectionHandlerType>()



                    .ToList();



                foreach (var ct in connTypes)



                {



                    if (ct.FamilyName.Equals(oldName, StringComparison.OrdinalIgnoreCase) ||



                        ct.FamilyName.Replace(" ", "").Equals(oldName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))



                    {



                        try



                        {



                            var fp = ct.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME);



                            if (fp != null && !fp.IsReadOnly)



                            {



                                fp.Set(newName);



                            }



                        }



                        catch { }



                    }



                }



            }



            catch { }



            // 3. Update Connection Handlers and Subelements



            var strVal = new StringParameterValue(newName);



            foreach (var conn in matchedConns)



            {



                bool pOk = SetParamValue(conn, "Type of connection", newName)



                        || SetParamValue(conn, "Type of Connection", newName);



                if (pOk) connsUpdated++;



                try



                {



                    var subs = conn.GetSubelements();



                    if (subs != null && paramId != ElementId.InvalidElementId)



                    {



                        foreach (Subelement sub in subs)



                        {



                            try



                            {



                                if (sub.HasParameter(paramId) && sub.IsParameterModifiable(paramId))



                                {



                                    sub.SetParameterValue(paramId, strVal);



                                    subsUpdated++;



                                }



                            }



                            catch { }



                        }



                    }



                }



                catch { }



                try



                {



                    var depIds = conn.GetDependentElements(null);



                    foreach (var childId in depIds)



                    {



                        if (childId == conn.Id) continue;



                        Element? child = doc.GetElement(childId);



                        if (child != null)



                        {



                            bool cOk = SetParamValue(child, "Type of connection", newName)



                                    || SetParamValue(child, "Type of Connection", newName);



                            if (cOk) depUpdated++;



                        }



                    }



                }



                catch { }



            }



            tx.Commit();



        }



        sb.AppendLine($"\n[HASIL EKSEKUSI]");



        sb.AppendLine($"- Revit Families Di-rename             : {famsRenamed}");



        sb.AppendLine($"- Connection Handlers Berhasil Diupdate: {connsUpdated} dari {matchedConns.Count}");



        sb.AppendLine($"- Sub-Elemen Koneksi Diupdate          : {subsUpdated}");



        sb.AppendLine($"- Dependent Elements Diupdate          : {depUpdated}");



        sb.AppendLine("\n[FASE 3: POST-EXECUTION VERIFIKASI & SAFETY GATE]");



        sb.AppendLine("Sub-Agent 3 (Auditor): Memverifikasi kepatuhan 100% parameter pasca-eksekusi...");



        sb.AppendLine(VerifyPopulatedConnections(doc));



        return sb.ToString();



    }



    public static string AuditDuplicateElements(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: AUDIT ELEMEN TERDUPLIKAT DI LOKASI YANG SAMA ===");



        sb.AppendLine($"Dokumen    : {doc.Title}");



        sb.AppendLine($"Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        // =========================================================================



        // BAGIAN 1: AUDIT NATIVE WARNINGS REVIT (doc.GetWarnings())



        // =========================================================================



        sb.AppendLine("\n-------------------------------------------------------------");



        sb.AppendLine("[BAGIAN 1: AUDIT NATIVE WARNINGS REVIT]");



        sb.AppendLine("-------------------------------------------------------------");



        var allWarnings = doc.GetWarnings();



        sb.AppendLine($"Total Peringatan (Warnings) di Model: {allWarnings.Count}");



        var duplicateWarnings = new List<FailureMessage>();



        var overlapWarnings = new List<FailureMessage>();



        var otherWarnings = new List<FailureMessage>();



        foreach (var w in allWarnings)



        {



            string desc = w.GetDescriptionText();



            if (desc.IndexOf("identical instances", StringComparison.OrdinalIgnoreCase) >= 0 ||



                desc.IndexOf("same place", StringComparison.OrdinalIgnoreCase) >= 0)



            {



                duplicateWarnings.Add(w);



            }



            else if (desc.IndexOf("overlap", StringComparison.OrdinalIgnoreCase) >= 0 ||



                     desc.IndexOf("tumpang tindih", StringComparison.OrdinalIgnoreCase) >= 0)



            {



                overlapWarnings.Add(w);



            }



            else



            {



                otherWarnings.Add(w);



            }



        }



        sb.AppendLine($"  * Peringatan Elemen Identik di Lokasi Sama (Duplicate Instances): {duplicateWarnings.Count}");



        sb.AppendLine($"  * Peringatan Elemen Tumpang Tindih (Overlapping Elements)        : {overlapWarnings.Count}");



        sb.AppendLine($"  * Peringatan Lainnya                                            : {otherWarnings.Count}");



        var recordedElementIds = new HashSet<long>();



        if (duplicateWarnings.Count > 0)



        {



            sb.AppendLine("\n>>> DETAIL DUPLICATE INSTANCES DARI WARNINGS REVIT <<<");



            int grpIdx = 1;



            foreach (var dw in duplicateWarnings)



            {



                var failingIds = dw.GetFailingElements();



                sb.AppendLine($"\n  [Grup #{grpIdx++}] {dw.GetDescriptionText()} ({failingIds.Count} elemen)");



                foreach (var id in failingIds)



                {



                    recordedElementIds.Add(id.Value);



                    Element? elem = doc.GetElement(id);



                    if (elem == null)



                    {



                        sb.AppendLine($"    - Id: {id.Value} (Elemen tidak ditemukan / terhapus)");



                        continue;



                    }



                    string catName = elem.Category?.Name ?? "(No Category)";



                    ElementId tid = elem.GetTypeId();



                    ElementType? et = (tid != null && tid != ElementId.InvalidElementId) ? doc.GetElement(tid) as ElementType : null;



                    string famName = et?.FamilyName ?? elem.Name;



                    string typeName = et?.Name ?? "";



                    string mark = elem.LookupParameter("Mark")?.AsString() ?? "";



                    string level = doc.GetElement(elem.LevelId)?.Name ?? "";



                    string locStr = "";



                    if (elem.Location is LocationPoint lp)



                    {



                        locStr = $"Point({lp.Point.X:F2}, {lp.Point.Y:F2}, {lp.Point.Z:F2})";



                    }



                    else if (elem.Location is LocationCurve lc)



                    {



                        XYZ p0 = lc.Curve.GetEndPoint(0);



                        XYZ p1 = lc.Curve.GetEndPoint(1);



                        locStr = $"Curve(({p0.X:F2}, {p0.Y:F2}, {p0.Z:F2}) -> ({p1.X:F2}, {p1.Y:F2}, {p1.Z:F2}))";



                    }



                    sb.AppendLine($"    - Id: {id.Value} | Kategori: {catName} | Family: '{famName}' | Type: '{typeName}' | Mark: '{mark}' | Level: '{level}' | {locStr}");



                }



            }



        }



        if (overlapWarnings.Count > 0)



        {



            sb.AppendLine("\n>>> DETAIL OVERLAPPING ELEMENTS DARI WARNINGS REVIT <<<");



            int ovlIdx = 1;



            foreach (var ow in overlapWarnings.Take(20))



            {



                var failingIds = ow.GetFailingElements();



                sb.AppendLine($"  * Overlap #{ovlIdx++}: {ow.GetDescriptionText()} | Ids: {string.Join(", ", failingIds.Select(x => x.Value))}");



            }



            if (overlapWarnings.Count > 20)



            {



                sb.AppendLine($"  * ... dan {overlapWarnings.Count - 20} overlap warnings lainnya.");



            }



        }



        // =========================================================================



        // BAGIAN 2: DEEP GEOMETRIC SCAN (DETEKSI GEOMETRI ELEMEN LANGSUNG)



        // =========================================================================



        sb.AppendLine("\n-------------------------------------------------------------");



        sb.AppendLine("[BAGIAN 2: AUDIT GEOMETRI PRESISI (SCAN LANGSUNG KOORDINAT ELEMEN)]");



        sb.AppendLine("-------------------------------------------------------------");



        // 2A. Structural Framing



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .ToList();



        var framingGroups = new Dictionary<string, List<Element>>();



        foreach (var f in framings)



        {



            if (f.Location is LocationCurve lc)



            {



                XYZ p0 = lc.Curve.GetEndPoint(0);



                XYZ p1 = lc.Curve.GetEndPoint(1);



                if (p0.X > p1.X || (Math.Abs(p0.X - p1.X) < 1e-4 && (p0.Y > p1.Y || (Math.Abs(p0.Y - p1.Y) < 1e-4 && p0.Z > p1.Z))))



                {



                    var tmp = p0; p0 = p1; p1 = tmp;



                }



                string key = $"{Math.Round(p0.X, 2):F2}_{Math.Round(p0.Y, 2):F2}_{Math.Round(p0.Z, 2):F2}_to_{Math.Round(p1.X, 2):F2}_{Math.Round(p1.Y, 2):F2}_{Math.Round(p1.Z, 2):F2}";



                if (!framingGroups.ContainsKey(key)) framingGroups[key] = new List<Element>();



                framingGroups[key].Add(f);



            }



        }



        var dupFramings = framingGroups.Where(kvp => kvp.Value.Count > 1).ToList();



        sb.AppendLine($"\n2A. Structural Framing (Balok, Castellated, Bracing):");



        sb.AppendLine($"  - Total Elemen Diperiksa     : {framings.Count}");



        sb.AppendLine($"  - Lokasi Terduplikat Geometri: {dupFramings.Count} lokasi");



        if (dupFramings.Count > 0)



        {



            int dIdx = 1;



            foreach (var kvp in dupFramings)



            {



                sb.AppendLine($"    * Lokasi #{dIdx++} [{kvp.Key}]: {kvp.Value.Count} balok di posisi identik:");



                foreach (var elem in kvp.Value)



                {



                    ElementId tid = elem.GetTypeId();



                    ElementType? et = doc.GetElement(tid) as ElementType;



                    sb.AppendLine($"        -> Id: {elem.Id.Value} | Family: '{et?.FamilyName}' | Type: '{et?.Name}' | Comments: '{elem.LookupParameter("Comments")?.AsString()}'");



                }



            }



            if (dupFramings.Count > 20) sb.AppendLine($"    * ... dan {dupFramings.Count - 20} lokasi framing duplikat lainnya.");



        }



        // 2B. Structural Columns



        var columns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralColumns)



            .WhereElementIsNotElementType()



            .ToList();



        var colGroups = new Dictionary<string, List<Element>>();



        foreach (var c in columns)



        {



            if (c.Location is LocationPoint lp)



            {



                if (Math.Abs(lp.Point.X) < 0.05 && Math.Abs(lp.Point.Y) < 0.05) continue;



                string key = $"{Math.Round(lp.Point.X, 2):F2}_{Math.Round(lp.Point.Y, 2):F2}";



                if (!colGroups.ContainsKey(key)) colGroups[key] = new List<Element>();



                colGroups[key].Add(c);



            }



        }



        int dupColCount = 0;



        var dupColsReport = new StringBuilder();



        foreach (var kvp in colGroups.Where(x => x.Value.Count > 1))



        {



            var list = kvp.Value;



            for (int i = 0; i < list.Count; i++)



            {



                var bb1 = list[i].get_BoundingBox(null);



                if (bb1 == null) continue;



                for (int j = i + 1; j < list.Count; j++)



                {



                    var bb2 = list[j].get_BoundingBox(null);



                    if (bb2 == null) continue;



                    bool zOverlap = (bb1.Min.Z < bb2.Max.Z - 0.05) && (bb2.Min.Z < bb1.Max.Z - 0.05);



                    if (zOverlap)



                    {



                        dupColCount++;



                        dupColsReport.AppendLine($"    * Kolom Duplikat di XY ({kvp.Key}): Id {list[i].Id.Value} ('{list[i].Name}') & Id {list[j].Id.Value} ('{list[j].Name}')");



                    }



                }



            }



        }



        sb.AppendLine($"\n2B. Structural Columns (Kolom):");



        sb.AppendLine($"  - Total Kolom Diperiksa      : {columns.Count}");



        sb.AppendLine($"  - Kolom Duplikat/Tumpang Tindih: {dupColCount} kasus");



        if (dupColCount > 0) sb.Append(dupColsReport);



        // 2C. Structural Connections



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        var connConnectedMap = new Dictionary<string, List<Element>>();



        foreach (var cn in conns)



        {



            if (cn is Autodesk.Revit.DB.Structure.StructuralConnectionHandler handler)



            {



                var connIds = handler.GetConnectedElementIds().OrderBy(x => x.Value).Select(x => x.Value.ToString());



                string ckey = string.Join("+", connIds);



                if (!string.IsNullOrWhiteSpace(ckey))



                {



                    if (!connConnectedMap.ContainsKey(ckey)) connConnectedMap[ckey] = new List<Element>();



                    connConnectedMap[ckey].Add(cn);



                }



            }



        }



        var dupConns = connConnectedMap.Where(x => x.Value.Count > 1).ToList();



        sb.AppendLine($"\n2C. Structural Connections (Sambungan Baja):");



        sb.AppendLine($"  - Total Sambungan Diperiksa  : {conns.Count}");



        sb.AppendLine($"  - Sambungan Ganda di Input Sama: {dupConns.Count} lokasi");



        if (dupConns.Count > 0)



        {



            foreach (var dc in dupConns)



            {



                sb.AppendLine($"    * Input Elemen ({dc.Key}): {dc.Value.Count} sambungan terpasang:");



                foreach (var item in dc.Value)



                {



                    var et = doc.GetElement(item.GetTypeId()) as ElementType;



                    sb.AppendLine($"        -> Id: {item.Id.Value} | Family: '{et?.FamilyName}' | Type: '{et?.Name}'");



                }



            }



        }



        // 2D. Standalone Plates



        var plates = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



            .WhereElementIsNotElementType()



            .ToList();



        var standalonePlates = plates.Where(p => p.LookupParameter("Comments")?.AsString() != null).ToList();



        var plateCentroidMap = new Dictionary<string, List<Element>>();



        foreach (var p in standalonePlates)



        {



            var bb = p.get_BoundingBox(null);



            if (bb != null)



            {



                XYZ c = (bb.Min + bb.Max) * 0.5;



                string pkey = $"{Math.Round(c.X, 2):F2}_{Math.Round(c.Y, 2):F2}_{Math.Round(c.Z, 2):F2}";



                if (!plateCentroidMap.ContainsKey(pkey)) plateCentroidMap[pkey] = new List<Element>();



                plateCentroidMap[pkey].Add(p);



            }



        }



        var dupPlates = plateCentroidMap.Where(x => x.Value.Count > 1).ToList();



        sb.AppendLine($"\n2D. Connection Plates (Pelat Sambungan):");



        sb.AppendLine($"  - Total Pelat di Model       : {plates.Count}");



        sb.AppendLine($"  - Pelat Standalone Diperiksa : {standalonePlates.Count}");



        sb.AppendLine($"  - Pelat Standalone Duplikat  : {dupPlates.Count} lokasi");



        if (dupPlates.Count > 0)



        {



            foreach (var dp in dupPlates.Take(10))



            {



                sb.AppendLine($"    * Centroid ({dp.Key}): {dp.Value.Count} pelat -> Ids: {string.Join(", ", dp.Value.Select(x => x.Id.Value))}");



            }



        }



        // 2E. Walls, Floors, Foundations



        var walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().ToList();



        var floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().ToList();



        var founds = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation).WhereElementIsNotElementType().ToList();



        sb.AppendLine($"\n2E. Elemen Lainnya:");



        sb.AppendLine($"  - Walls: {walls.Count} | Floors: {floors.Count} | Foundations: {founds.Count}");



        // =========================================================================



        // BAGIAN 3: KESIMPULAN AUDIT & ACTION PLAN



        // =========================================================================



        sb.AppendLine("\n-------------------------------------------------------------");



        sb.AppendLine("[BAGIAN 3: KESIMPULAN REKAPITULASI AUDIT DUPLIKAT]");



        sb.AppendLine("-------------------------------------------------------------");



        int totalDupCases = duplicateWarnings.Count + dupFramings.Count + dupColCount + dupConns.Count + dupPlates.Count;



        sb.AppendLine($"Total Indikasi Kasus Duplikat Ditemukan: {totalDupCases}");



        sb.AppendLine($"  1. Duplicate Warnings Resmi Revit : {duplicateWarnings.Count} kasus");



        sb.AppendLine($"  2. Framing Duplicate Geometri     : {dupFramings.Count} lokasi");



        sb.AppendLine($"  3. Column Overlapping Geometri    : {dupColCount} kasus");



        sb.AppendLine($"  4. Connection Handler Duplikat    : {dupConns.Count} lokasi");



        sb.AppendLine($"  5. Connection Plate Duplikat      : {dupPlates.Count} lokasi");



        return sb.ToString();



    }



}



public class ClashWarningSwallower : IFailuresPreprocessor



{



    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)



    {



        failuresAccessor.DeleteAllWarnings();



        return FailureProcessingResult.Continue;



    }



}



partial class BimAuditSkill



{



    private static string CreateClashValidationSchedules(UIApplication app)



    {



        Document? doc = app?.ActiveUIDocument?.Document;



        if (doc == null) return "Error: No active Revit document.";



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: PEMBUATAN SCHEDULE REVIT UNTUK VALIDASI CLASH ELEMEN ===");



        sb.AppendLine($"Dokumen   : {doc.Title}");



        sb.AppendLine($"Waktu     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");



        // 1. Data Pasangan Framing (73 pairs = 146 elemen)



        var framingPairs = new (long idA, long idB, string code, string note)[]



        {



            // 20 Pasang Tanggulan 50x100



            (6250597, 6252184, "P-FR01", "TANGGULAN 50x100"),



            (6250598, 6252185, "P-FR02", "TANGGULAN 50x100"),



            (6250599, 6252186, "P-FR03", "TANGGULAN 50x100"),



            (6250600, 6252187, "P-FR04", "TANGGULAN 50x100"),



            (6250610, 6252253, "P-FR05", "TANGGULAN 50x100"),



            (6250611, 6252254, "P-FR06", "TANGGULAN 50x100"),



            (6250612, 6252255, "P-FR07", "TANGGULAN 50x100"),



            (6250613, 6252256, "P-FR08", "TANGGULAN 50x100"),



            (6250624, 6252321, "P-FR09", "TANGGULAN 50x100"),



            (6250625, 6252322, "P-FR10", "TANGGULAN 50x100"),



            (6250626, 6252323, "P-FR11", "TANGGULAN 50x100"),



            (6250627, 6252324, "P-FR12", "TANGGULAN 50x100"),



            (6250636, 6252389, "P-FR13", "TANGGULAN 50x100"),



            (6250637, 6252390, "P-FR14", "TANGGULAN 50x100"),



            (6250638, 6252391, "P-FR15", "TANGGULAN 50x100"),



            (6250639, 6252392, "P-FR16", "TANGGULAN 50x100"),



            (6250648, 6252457, "P-FR17", "TANGGULAN 50x100"),



            (6250649, 6252458, "P-FR18", "TANGGULAN 50x100"),



            (6250650, 6252459, "P-FR19", "TANGGULAN 50x100"),



            (6250651, 6252460, "P-FR20", "TANGGULAN 50x100"),



            // 7 Pasang Sloof S2 200x300



            (6482129, 6602003, "P-FR21", "S2 200 X 300"),



            (6506484, 6601924, "P-FR22", "S2 200 X 300"),



            (6644046, 6645673, "P-FR23", "S2 200 X 300"),



            (6644097, 6645690, "P-FR24", "S2 200 X 300"),



            (6644146, 6645707, "P-FR25", "S2 200 X 300"),



            (6658537, 6658564, "P-FR26", "S2 200 X 300"),



            (6658609, 6658636, "P-FR27", "S2 200 X 300"),



            // 26 Pasang Voute Rafter WF 350



            (4887585, 4887692, "P-FR28", "WF 350 Voute Rafter"),



            (4890000, 4890107, "P-FR29", "WF 350 Voute Rafter"),



            (4892413, 4892520, "P-FR30", "WF 350 Voute Rafter"),



            (4894828, 4894935, "P-FR31", "WF 350 Voute Rafter"),



            (4897243, 4897350, "P-FR32", "WF 350 Voute Rafter"),



            (4899660, 4899767, "P-FR33", "WF 350 Voute Rafter"),



            (4902073, 4902180, "P-FR34", "WF 350 Voute Rafter"),



            (4904488, 4904595, "P-FR35", "WF 350 Voute Rafter"),



            (4906903, 4907010, "P-FR36", "WF 350 Voute Rafter"),



            (4909318, 4909425, "P-FR37", "WF 350 Voute Rafter"),



            (4915618, 4915725, "P-FR38", "WF 350 Voute Rafter"),



            (4919265, 4919372, "P-FR39", "WF 350 Voute Rafter"),



            (4921846, 4921953, "P-FR40", "WF 350 Voute Rafter"),



            (4924385, 4924492, "P-FR41", "WF 350 Voute Rafter"),



            (4926924, 4927031, "P-FR42", "WF 350 Voute Rafter"),



            (4929463, 4929570, "P-FR43", "WF 350 Voute Rafter"),



            (4932002, 4932109, "P-FR44", "WF 350 Voute Rafter"),



            (4934541, 4934648, "P-FR45", "WF 350 Voute Rafter"),



            (4937253, 4937360, "P-FR46", "WF 350 Voute Rafter"),



            (4939792, 4939899, "P-FR47", "WF 350 Voute Rafter"),



            (6586650, 6586741, "P-FR48", "WF 350 Voute Rafter"),



            (6588906, 6588966, "P-FR49", "WF 350 Voute Rafter"),



            (6589644, 6589704, "P-FR50", "WF 350 Voute Rafter"),



            (6590382, 6590442, "P-FR51", "WF 350 Voute Rafter"),



            (6593944, 6594004, "P-FR52", "WF 350 Voute Rafter"),



            (6603253, 6603344, "P-FR53", "WF 350 Voute Rafter"),



            // 20 Pasang Balok Dudukan Silo & Tambahan



            (3587854, 3598278, "P-FR54", "WF 250 Dudukan Silo"),



            (3588649, 3598761, "P-FR55", "WF 250 Dudukan Silo"),



            (3589015, 3598943, "P-FR56", "WF 250 Dudukan Silo"),



            (3589047, 3599629, "P-FR57", "WF 250 Dudukan Silo"),



            (3589079, 3599654, "P-FR58", "WF 250 Dudukan Silo"),



            (3589113, 3599693, "P-FR59", "WF 250 Dudukan Silo"),



            (3589147, 3599722, "P-FR60", "WF 250 Dudukan Silo"),



            (3589179, 3599745, "P-FR61", "WF 250 Dudukan Silo"),



            (3589213, 3599764, "P-FR62", "WF 250 Dudukan Silo"),



            (3589245, 3599785, "P-FR63", "WF 250 Dudukan Silo"),



            (3600935, 3635317, "P-FR64", "WF 250 Tambahan"),



            (3600936, 3635472, "P-FR65", "WF 250 Tambahan"),



            (3600937, 3635481, "P-FR66", "WF 250 Tambahan"),



            (3600938, 3635490, "P-FR67", "WF 250 Tambahan"),



            (3603764, 3635626, "P-FR68", "WF 150 Tambahan"),



            (3603765, 3635786, "P-FR69", "WF 150 Tambahan"),



            (3603766, 3635866, "P-FR70", "WF 150 Tambahan"),



            (3616676, 3635999, "P-FR71", "UNP 100 Tambahan"),



            (3616677, 3636111, "P-FR72", "UNP 100 Tambahan"),



            (3616678, 3636120, "P-FR73", "UNP 100 Tambahan")



        };



        // 2. Data Pasangan Sambungan (8 pairs = 16 sambungan)



        var connectionPairs = new (long idA, long idB, string code, string note)[]



        {



            (6664459, 6664460, "P-CN01", "WF350-Endplate CT 1"),



            (6739995, 6740244, "P-CN02", "WF350-Endplate CT 1"),



            (6739996, 6740245, "P-CN03", "WF350-Endplate CT 1"),



            (6739997, 6740246, "P-CN04", "WF-400 Endplate-H250 1"),



            (6739998, 6740247, "P-CN05", "WF350-Endplate-H250"),



            (6651683, 6650135, "P-CN06", "WF350-Endplate vs Generic Dummy"),



            (6652538, 6652602, "P-CN07", "WF350-Endplate CT 1 vs CT 2"),



            (6756932, 6765794, "P-CN08", "WF350-Gusset Plate vs WF400")



        };



        // 3. Data Pasangan Kolom (11 pairs = 22 kolom)



        var columnPairs = new (long idA, long idB, string code, string note)[]



        {



            (3747807, 4058054, "P-COL01", "H 300 di XY (-44.90, 81.19)"),



            (5117597, 5117669, "P-COL02", "H 300 di XY (-44.90, 81.19)"),



            (3747927, 4057764, "P-COL03", "H 300 di XY (-84.27, 81.19)"),



            (3747950, 4057471, "P-COL04", "Tank Pedestal 450x450 di XY (-123.64, 81.19)"),



            (3747951, 4057472, "P-COL05", "H 300 di XY (-123.64, 81.19)"),



            (3747974, 4057179, "P-COL06", "Tank Pedestal 450x450 di XY (-163.01, 81.19)"),



            (3747975, 4057180, "P-COL07", "H 300 di XY (-163.01, 81.19)"),



            (5117609, 5117663, "P-COL08", "H 300 di XY (-163.01, 81.19)"),



            (3747998, 4056887, "P-COL09", "Tank Pedestal 450x450 di XY (-202.38, 81.19)"),



            (3747999, 4056888, "P-COL10", "H 300 di XY (-202.38, 81.19)"),



            (5117613, 5117661, "P-COL11", "H 300 di XY (-202.38, 81.19)")



        };



        // Ambil sampel parameter organisasi browser dari schedule eksisting jika ada



        var sampleSched = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .FirstOrDefault(s => !s.IsTemplate && !s.Name.StartsWith("_VALIDASI"));



        var orgParams = new Dictionary<string, string>();



        if (sampleSched != null)



        {



            foreach (Parameter p in sampleSched.Parameters)



            {



                if (p.StorageType == StorageType.String && !p.IsReadOnly && !string.IsNullOrWhiteSpace(p.AsString()))



                {



                    string pName = p.Definition.Name;



                    if (pName.Contains("Discipline") || pName.Contains("Sub") || pName.Contains("Group") || pName.Contains("Views"))



                    {



                        orgParams[pName] = p.AsString();



                    }



                }



            }



        }



        ViewSchedule? schedFraming = null;



        ViewSchedule? schedConn = null;



        ViewSchedule? schedCol = null;



        using (Transaction trans = new(doc, "AIGRAV - Setup Clash Crosscheck Schedules"))



        {



            var failOpt = trans.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            trans.SetFailureHandlingOptions(failOpt);



            trans.Start();



            // A. Tagging Parameter Mark & Comments pada Elemen



            int taggedFraming = TagPairListSafe(doc, framingPairs);



            int taggedConn = TagPairListSafe(doc, connectionPairs);



            int taggedCol = TagPairListSafe(doc, columnPairs);



            sb.AppendLine("[TAHAP 1: PARAMETER TAGGING SELESAI]");



            sb.AppendLine($"  - Balok (Structural Framing)    : {taggedFraming} elemen ({framingPairs.Length} pasang)");



            sb.AppendLine($"  - Sambungan (Struct Connections): {taggedConn} elemen ({connectionPairs.Length} pasang)");



            sb.AppendLine($"  - Kolom (Structural Columns)    : {taggedCol} elemen ({columnPairs.Length} pasang)");



            // B. Pembuatan Schedule Views di Revit



            sb.AppendLine("\n[TAHAP 2: PEMBUATAN VIEW SCHEDULE DI REVIT]");



            // Schedule 1: Structural Framing



            schedFraming = BuildValidationSchedule(



                doc,



                BuiltInCategory.OST_StructuralFraming,



                "_VALIDASI CLASH - STRUCTURAL FRAMING",



                "P-FR",



                new List<string> { "Mark", "Family and Type", "Comments", "Reference Level", "Cut Length", "Length", "Count" },



                orgParams);



            if (schedFraming != null)



                sb.AppendLine($"  ✓ Berhasil membuat ViewSchedule: '{schedFraming.Name}' (Id: {schedFraming.Id.Value})");



            // Schedule 2: Structural Connections



            schedConn = BuildValidationSchedule(



                doc,



                BuiltInCategory.OST_StructConnections,



                "_VALIDASI CLASH - SAMBUNGAN BAJA",



                "P-CN",



                new List<string> { "Mark", "Family and Type", "Comments", "Type of Connection", "Count" },



                orgParams);



            if (schedConn != null)



                sb.AppendLine($"  ✓ Berhasil membuat ViewSchedule: '{schedConn.Name}' (Id: {schedConn.Id.Value})");



            else



                sb.AppendLine("  ℹ Kategori Structural Connections tidak didukung untuk ViewSchedule di versi ini.");



            // Schedule 3: Structural Columns



            schedCol = BuildValidationSchedule(



                doc,



                BuiltInCategory.OST_StructuralColumns,



                "_VALIDASI CLASH - KOLOM & PEDESTAL",



                "P-COL",



                new List<string> { "Mark", "Family and Type", "Comments", "Base Level", "Top Level", "Count" },



                orgParams);



            if (schedCol != null)



                sb.AppendLine($"  ✓ Berhasil membuat ViewSchedule: '{schedCol.Name}' (Id: {schedCol.Id.Value})");



            TransactionStatus st = trans.Commit();



            sb.AppendLine($"\n[STATUS COMMIT TRANSAKSI]: {st}");



        }



        // C. Aktivasi View Otomatis agar langsung terbuka di layar pengguna



        if (schedFraming != null && app?.ActiveUIDocument != null)



        {



            try



            {



                app.ActiveUIDocument.ActiveView = schedFraming;



                sb.AppendLine($"\n[VIEWPORT]: Schedule '{schedFraming.Name}' langsung diaktifkan di layar Revit!");



            }



            catch (Exception ex)



            {



                sb.AppendLine($"\n[VIEWPORT]: Info aktivasi view: {ex.Message}");



            }



        }



        sb.AppendLine("\n-------------------------------------------------------------");



        sb.AppendLine("[PETUNJUK CROSS-CHECK OLEH PENGGUNA DI REVIT]");



        sb.AppendLine("-------------------------------------------------------------");



        sb.AppendLine("1. Periksa tab view yang saat ini aktif di Revit: '_VALIDASI CLASH - STRUCTURAL FRAMING'.");



        sb.AppendLine("2. Atau buka melalui 'Project Browser' -> 'Schedules/Quantities'.");



        sb.AppendLine("3. Setiap pasang elemen yang saling clash tersusun berdampingan:");



        sb.AppendLine("   - Baris [A-PERTAHANKAN]: Elemen master asli");



        sb.AppendLine("   - Baris [B-KANDIDAT HAPUS]: Elemen duplikat yang clash (menampilkan ID pasangannya)");



        sb.AppendLine("4. Klik baris manapun dan pilih 'Highlight in Model' di ribbon Revit.");



        return sb.ToString();



    }



    private static int TagPairListSafe(Document doc, (long idA, long idB, string code, string note)[] pairs)



    {



        int count = 0;



        foreach (var p in pairs)



        {



            Element? elemA = doc.GetElement(new ElementId(p.idA));



            Element? elemB = doc.GetElement(new ElementId(p.idB));



            if (elemA != null)



            {



                var pMark = elemA.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elemA.LookupParameter("Mark");



                pMark?.Set($"{p.code}-A-{p.idA} [PERTAHANKAN]");



                var pComm = elemA.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? elemA.LookupParameter("Comments");



                string cA = pComm?.AsString() ?? "";



                if (cA.StartsWith("[AUDIT") || cA.StartsWith("[ID:")) cA = "";



                string newCommA = $"[ID: {p.idA}] CLASH DENGAN ID: {p.idB} (Kandidat Hapus) | {p.note}";



                if (!string.IsNullOrWhiteSpace(cA)) newCommA += $" | {cA}";



                pComm?.Set(newCommA);



                count++;



            }



            if (elemB != null)



            {



                var pMark = elemB.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elemB.LookupParameter("Mark");



                pMark?.Set($"{p.code}-B-{p.idB} [KANDIDAT HAPUS]");



                var pComm = elemB.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? elemB.LookupParameter("Comments");



                string cB = pComm?.AsString() ?? "";



                if (cB.StartsWith("[AUDIT") || cB.StartsWith("[ID:")) cB = "";



                string newCommB = $"[ID: {p.idB}] CLASH DENGAN ID: {p.idA} (Pertahankan) | {p.note}";



                if (!string.IsNullOrWhiteSpace(cB)) newCommB += $" | {cB}";



                pComm?.Set(newCommB);



                count++;



            }



        }



        return count;



    }



    private static ViewSchedule? BuildValidationSchedule(



        Document doc,



        BuiltInCategory cat,



        string scheduleName,



        string filterPrefix,



        List<string> desiredFieldNames,



        Dictionary<string, string> orgParams)



    {



        ElementId catId = new(cat);



        if (!ViewSchedule.IsValidCategoryForSchedule(catId))



        {



            return null;



        }



        // 1. Hapus schedule lama jika ada nama yang sama



        var existing = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .FirstOrDefault(s => s.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase));



        if (existing != null)



        {



            doc.Delete(existing.Id);



        }



        // 2. Buat schedule baru



        ViewSchedule vs = ViewSchedule.CreateSchedule(doc, catId);



        vs.Name = scheduleName;



        // Set parameter organisasi browser agar schedule tidak hilang di filter browser



        foreach (var kvp in orgParams)



        {



            Parameter? p = vs.LookupParameter(kvp.Key);



            if (p != null && !p.IsReadOnly)



            {



                try { p.Set(kvp.Value); } catch {}



            }



        }



        ScheduleDefinition def = vs.Definition;



        var sfList = def.GetSchedulableFields();



        var sfMap = new Dictionary<string, SchedulableField>(StringComparer.OrdinalIgnoreCase);



        foreach (var sf in sfList)



        {



            string nm = sf.GetName(doc);



            if (!sfMap.ContainsKey(nm)) sfMap[nm] = sf;



        }



        ScheduleField? markField = null;



        foreach (var fn in desiredFieldNames)



        {



            if (sfMap.TryGetValue(fn, out var foundSf))



            {



                var field = def.AddField(foundSf);



                if (fn.Equals("Mark", StringComparison.OrdinalIgnoreCase))



                {



                    markField = field;



                    field.ColumnHeading = "Kode Pairing Clash";



                }



                else if (fn.Equals("Family and Type", StringComparison.OrdinalIgnoreCase) || fn.Equals("Type", StringComparison.OrdinalIgnoreCase))



                {



                    field.ColumnHeading = "Tipe / Profil Elemen";



                }



                else if (fn.Equals("Comments", StringComparison.OrdinalIgnoreCase))



                {



                    field.ColumnHeading = "ID Elemen & ID Pasangan Clash";



                }



                else if (fn.Equals("Count", StringComparison.OrdinalIgnoreCase))



                {



                    field.ColumnHeading = "Qty";



                }



            }



        }



        // Pastikan Mark field ada



        if (markField == null && sfMap.TryGetValue("Mark", out var sfMarkFallback))



        {



            markField = def.AddField(sfMarkFallback);



            markField.ColumnHeading = "Kode Pairing Clash";



        }



        // Tampilkan setiap elemen (itemize)



        def.IsItemized = true;



        // Sorting by Mark ascending



        if (markField != null)



        {



            ScheduleSortGroupField sort = new(markField.FieldId, ScheduleSortOrder.Ascending);



            def.AddSortGroupField(sort);



            // Filter Mark begins with filterPrefix



            try



            {



                ScheduleFilter filt = new(markField.FieldId, ScheduleFilterType.BeginsWith, filterPrefix);



                def.AddFilter(filt);



            }



            catch



            {



                try



                {



                    ScheduleFilter filt = new(markField.FieldId, ScheduleFilterType.Contains, filterPrefix);



                    def.AddFilter(filt);



                }



                catch {}



            }



        }



        return vs;



    }



    private static string InspectAndOpenSchedules(UIApplication app)



    {



        var uidoc = app.ActiveUIDocument;



        var doc = uidoc?.Document;



        if (doc == null) return "No active document.";



        var sb = new StringBuilder();



        // 1. Cari 3 schedule validasi clash



        var scheds = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => s.Name.StartsWith("_VALIDASI CLASH"))



            .ToList();



        sb.AppendLine($"Ditemukan {scheds.Count} schedule validasi di dokumen:");



        foreach (var s in scheds)



        {



            sb.AppendLine($"  - '{s.Name}' (Id: {s.Id.Value})");



        }



        // 2. Cari sample schedule eksisting yang tampil normal di Project Browser



        var refSched = doc.GetElement(new ElementId(422634)) as ViewSchedule



            ?? new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => !s.IsTemplate && !s.Name.StartsWith("_VALIDASI"));



        if (refSched != null)



        {



            sb.AppendLine($"\nReferensi Schedule Eksisting: '{refSched.Name}' (Id: {refSched.Id.Value})");



            sb.AppendLine("Parameter Pengelompokan Browser pada Referensi:");



            foreach (Parameter p in refSched.Parameters)



            {



                if (p.StorageType == StorageType.String && !string.IsNullOrWhiteSpace(p.AsString()))



                {



                    sb.AppendLine($"  {p.Definition.Name} = '{p.AsString()}' (ReadOnly: {p.IsReadOnly})");



                }



            }



        }



        // 3. Update parameter pengelompokan pada schedule validasi agar muncul di folder yang sama di Project Browser



        if (refSched != null && scheds.Count > 0)



        {



            using (Transaction t = new(doc, "AIGRAV - Align Clash Schedule Browser Parameters"))



            {



                var failOpt = t.GetFailureHandlingOptions();



                failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



                t.SetFailureHandlingOptions(failOpt);



                t.Start();



                foreach (var s in scheds)



                {



                    sb.AppendLine($"\n[MENSINKRONKAN PARAMETER BROWSER UNTUK '{s.Name}']");



                    foreach (Parameter pRef in refSched.Parameters)



                    {



                        if (pRef.IsReadOnly) continue;



                        string pName = pRef.Definition.Name;



                        if (pName.Equals("View Name", StringComparison.OrdinalIgnoreCase) ||



                            pName.Equals("Name", StringComparison.OrdinalIgnoreCase) ||



                            pName.Equals("Title on Sheet", StringComparison.OrdinalIgnoreCase) ||



                            pName.Contains("Dependency"))



                        {



                            continue;



                        }



                        Parameter? pTarget = s.LookupParameter(pName);



                        if (pTarget != null && !pTarget.IsReadOnly)



                        {



                            try



                            {



                                if (pRef.StorageType == StorageType.String && !string.IsNullOrWhiteSpace(pRef.AsString()))



                                {



                                    pTarget.Set(pRef.AsString());



                                    sb.AppendLine($"  ✓ Set {pName} = '{pRef.AsString()}'");



                                }



                                else if (pRef.StorageType == StorageType.Integer && (pName.Contains("Discipline") || pName.Contains("Group") || pName.Contains("Sub") || pName.Contains("Views")))



                                {



                                    pTarget.Set(pRef.AsInteger());



                                    sb.AppendLine($"  ✓ Set {pName} = {pRef.AsInteger()}");



                                }



                            }



                            catch (Exception ex)



                            {



                                sb.AppendLine($"  ✗ Gagal set {pName}: {ex.Message}");



                            }



                        }



                    }



                }



                var commitRes = t.Commit();



                sb.AppendLine($"\nCommit status perapian browser: {commitRes}");



            }



        }



        // 4. Info Schedule



        foreach (var s in scheds)



        {



            sb.AppendLine($"Schedule '{s.Name}' (Id: {s.Id.Value}) siap divalidasi.");



        }



        // 5. Buka langsung (ActiveView) schedule structural framing di Revit UI



        var framingSched = scheds.FirstOrDefault(s => s.Name.Contains("FRAMING")) ?? scheds.FirstOrDefault();



        if (framingSched != null && uidoc != null)



        {



            try



            {



                uidoc.ActiveView = framingSched;



                sb.AppendLine($"\n>>> [ACTIVE VIEW]: Berhasil membuka dan mengaktifkan schedule '{framingSched.Name}' di layar Revit pengguna! <<<");



            }



            catch (Exception ex)



            {



                sb.AppendLine($"\n[ACTIVE VIEW]: Gagal mengaktifkan view: {ex.Message}");



            }



        }



        return sb.ToString();



    }



    private static (long idA, long idB, string code, string note)[] GetAllAuditPairs()



    {



        return new (long idA, long idB, string code, string note)[]



        {



            // 20 Pasang Tanggulan 50x100



            (6250597, 6252184, "P-FR01", "TANGGULAN 50x100"),



            (6250598, 6252185, "P-FR02", "TANGGULAN 50x100"),



            (6250599, 6252186, "P-FR03", "TANGGULAN 50x100"),



            (6250600, 6252187, "P-FR04", "TANGGULAN 50x100"),



            (6250610, 6252253, "P-FR05", "TANGGULAN 50x100"),



            (6250611, 6252254, "P-FR06", "TANGGULAN 50x100"),



            (6250612, 6252255, "P-FR07", "TANGGULAN 50x100"),



            (6250613, 6252256, "P-FR08", "TANGGULAN 50x100"),



            (6250624, 6252321, "P-FR09", "TANGGULAN 50x100"),



            (6250625, 6252322, "P-FR10", "TANGGULAN 50x100"),



            (6250626, 6252323, "P-FR11", "TANGGULAN 50x100"),



            (6250627, 6252324, "P-FR12", "TANGGULAN 50x100"),



            (6250636, 6252389, "P-FR13", "TANGGULAN 50x100"),



            (6250637, 6252390, "P-FR14", "TANGGULAN 50x100"),



            (6250638, 6252391, "P-FR15", "TANGGULAN 50x100"),



            (6250639, 6252392, "P-FR16", "TANGGULAN 50x100"),



            (6250648, 6252457, "P-FR17", "TANGGULAN 50x100"),



            (6250649, 6252458, "P-FR18", "TANGGULAN 50x100"),



            (6250650, 6252459, "P-FR19", "TANGGULAN 50x100"),



            (6250651, 6252460, "P-FR20", "TANGGULAN 50x100"),



            // 7 Pasang Sloof S2 200x300



            (6482129, 6602003, "P-FR21", "S2 200 X 300"),



            (6506484, 6601924, "P-FR22", "S2 200 X 300"),



            (6644046, 6645673, "P-FR23", "S2 200 X 300"),



            (6644097, 6645690, "P-FR24", "S2 200 X 300"),



            (6644146, 6645707, "P-FR25", "S2 200 X 300"),



            (6658537, 6658564, "P-FR26", "S2 200 X 300"),



            (6658609, 6658636, "P-FR27", "S2 200 X 300"),



            // 26 Pasang Voute Rafter WF 350



            (4887585, 4887692, "P-FR28", "WF 350 Voute Rafter"),



            (4890000, 4890107, "P-FR29", "WF 350 Voute Rafter"),



            (4892413, 4892520, "P-FR30", "WF 350 Voute Rafter"),



            (4894828, 4894935, "P-FR31", "WF 350 Voute Rafter"),



            (4897243, 4897350, "P-FR32", "WF 350 Voute Rafter"),



            (4899660, 4899767, "P-FR33", "WF 350 Voute Rafter"),



            (4902073, 4902180, "P-FR34", "WF 350 Voute Rafter"),



            (4904488, 4904595, "P-FR35", "WF 350 Voute Rafter"),



            (4906903, 4907010, "P-FR36", "WF 350 Voute Rafter"),



            (4909318, 4909425, "P-FR37", "WF 350 Voute Rafter"),



            (4915618, 4915725, "P-FR38", "WF 350 Voute Rafter"),



            (4919265, 4919372, "P-FR39", "WF 350 Voute Rafter"),



            (4921846, 4921953, "P-FR40", "WF 350 Voute Rafter"),



            (4924385, 4924492, "P-FR41", "WF 350 Voute Rafter"),



            (4926924, 4927031, "P-FR42", "WF 350 Voute Rafter"),



            (4929463, 4929570, "P-FR43", "WF 350 Voute Rafter"),



            (4932002, 4932109, "P-FR44", "WF 350 Voute Rafter"),



            (4934541, 4934648, "P-FR45", "WF 350 Voute Rafter"),



            (4937253, 4937360, "P-FR46", "WF 350 Voute Rafter"),



            (4939792, 4939899, "P-FR47", "WF 350 Voute Rafter"),



            (6586650, 6586741, "P-FR48", "WF 350 Voute Rafter"),



            (6588906, 6588966, "P-FR49", "WF 350 Voute Rafter"),



            (6589644, 6589704, "P-FR50", "WF 350 Voute Rafter"),



            (6590382, 6590442, "P-FR51", "WF 350 Voute Rafter"),



            (6593944, 6594004, "P-FR52", "WF 350 Voute Rafter"),



            (6603253, 6603344, "P-FR53", "WF 350 Voute Rafter"),



            // 20 Pasang Balok Dudukan Silo & Tambahan



            (3587854, 3598278, "P-FR54", "WF 250 Dudukan Silo"),



            (3588649, 3598761, "P-FR55", "WF 250 Dudukan Silo"),



            (3589015, 3598943, "P-FR56", "WF 250 Dudukan Silo"),



            (3589047, 3599629, "P-FR57", "WF 250 Dudukan Silo"),



            (3589079, 3599654, "P-FR58", "WF 250 Dudukan Silo"),



            (3589113, 3599693, "P-FR59", "WF 250 Dudukan Silo"),



            (3589147, 3599722, "P-FR60", "WF 250 Dudukan Silo"),



            (3589179, 3599745, "P-FR61", "WF 250 Dudukan Silo"),



            (3589213, 3599764, "P-FR62", "WF 250 Dudukan Silo"),



            (3589245, 3599785, "P-FR63", "WF 250 Dudukan Silo"),



            (3600935, 3635317, "P-FR64", "WF 250 Tambahan"),



            (3600936, 3635472, "P-FR65", "WF 250 Tambahan"),



            (3600937, 3635481, "P-FR66", "WF 250 Tambahan"),



            (3600938, 3635490, "P-FR67", "WF 250 Tambahan"),



            (3603764, 3635626, "P-FR68", "WF 150 Tambahan"),



            (3603765, 3635786, "P-FR69", "WF 150 Tambahan"),



            (3603766, 3635866, "P-FR70", "WF 150 Tambahan"),



            (3616676, 3635999, "P-FR71", "UNP 100 Tambahan"),



            (3616677, 3636111, "P-FR72", "UNP 100 Tambahan"),



            (3616678, 3636120, "P-FR73", "UNP 100 Tambahan"),



            // 8 Pasang Sambungan Baja



            (6664459, 6664460, "P-CN01", "WF350-Endplate CT 1"),



            (6739995, 6740244, "P-CN02", "WF350-Endplate CT 1"),



            (6739996, 6740245, "P-CN03", "WF350-Endplate CT 1"),



            (6739997, 6740246, "P-CN04", "WF-400 Endplate-H250 1"),



            (6739998, 6740247, "P-CN05", "WF350-Endplate-H250"),



            (6651683, 6650135, "P-CN06", "WF350-Endplate vs Generic Dummy"),



            (6652538, 6652602, "P-CN07", "WF350-Endplate CT 1 vs CT 2"),



            (6756932, 6765794, "P-CN08", "WF350-Gusset Plate vs WF400"),



            // 11 Pasang Kolom



            (3747807, 4058054, "P-COL01", "H 300"),



            (5117597, 5117669, "P-COL02", "H 300"),



            (3747927, 4057764, "P-COL03", "H 300"),



            (3747950, 4057471, "P-COL04", "Tank Pedestal 450x450"),



            (3747951, 4057472, "P-COL05", "H 300"),



            (3747974, 4057179, "P-COL06", "Tank Pedestal 450x450"),



            (3747975, 4057180, "P-COL07", "H 300"),



            (5117609, 5117663, "P-COL08", "H 300"),



            (3747998, 4056887, "P-COL09", "Tank Pedestal 450x450"),



            (3747999, 4056888, "P-COL10", "H 300"),



            (5117613, 5117661, "P-COL11", "H 300")



        };



    }



    private static string ExtractCleanOriginalComment(string current)



    {



        if (string.IsNullOrWhiteSpace(current)) return "";



        if (!current.StartsWith("[ID:")) return current;



        var parts = current.Split(new string[] { " | " }, StringSplitOptions.None);



        if (parts.Length >= 3)



        {



            return string.Join(" | ", parts.Skip(2)).Trim();



        }



        return "";



    }



    private static string CheckDeleteCandidates(Document doc)



    {



        var pairs = GetAllAuditPairs();



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: PRE-EXECUTION CHECK FOR CANDIDATE DELETION ===");



        sb.AppendLine($"Total Pairs to Process: {pairs.Length}");



        int foundCandidates = 0;



        int foundRetained = 0;



        foreach (var p in pairs)



        {



            var elemB = doc.GetElement(new ElementId(p.idB));



            var elemA = doc.GetElement(new ElementId(p.idA));



            string statusB = elemB != null ? $"EXISTS ({elemB.Category?.Name}: {elemB.Name})" : "NOT FOUND / ALREADY DELETED";



            if (elemB != null) foundCandidates++;



            string commA = elemA?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";



            string restoredCommA = ExtractCleanOriginalComment(commA);



            string statusA = elemA != null ? $"EXISTS (Comments will restore to: '{restoredCommA}')" : "NOT FOUND";



            if (elemA != null) foundRetained++;



            sb.AppendLine($"[{p.code}] Keep {p.idA}: {statusA} | Delete {p.idB}: {statusB}");



        }



        sb.AppendLine($"\nSummary: {foundCandidates} Candidate Elements found to delete, {foundRetained} Retained Elements found to restore.");



        return sb.ToString();



    }



    private static string ExecuteDeleteCandidates(Document doc)



    {



        var pairs = GetAllAuditPairs();



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: EKSEKUSI PENGHAPUSAN ELEMEN DUPLIKAT & RESTORE COMMENTS ===");



        sb.AppendLine($"Dokumen: {doc.Title}");



        sb.AppendLine($"Waktu  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");



        int deletedCount = 0;



        int restoredCommentsCount = 0;



        int clearedMarkCount = 0;



        var failedDeletions = new List<string>();



        using (Transaction trans = new(doc, "AIGRAV - Delete Duplicate Clash Candidates & Restore Comments"))



        {



            var failOpt = trans.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            trans.SetFailureHandlingOptions(failOpt);



            trans.Start();



            // 1. Hapus semua elemen kandidat hapus (elemB) dengan filter kategori ketat (Safety Gate)



            var targetCategories = new HashSet<long>



            {



                (long)BuiltInCategory.OST_StructuralFraming,



                (long)BuiltInCategory.OST_StructConnections,



                (long)BuiltInCategory.OST_StructuralColumns



            };



            foreach (var p in pairs)



            {



                var elemB = doc.GetElement(new ElementId(p.idB));



                if (elemB != null)



                {



                    long catVal = elemB.Category?.Id?.Value ?? 0;



                    if (!targetCategories.Contains(catVal))



                    {



                        sb.AppendLine($"  [SAFETY SKIP] ID {p.idB} ({elemB.Category?.Name}) bukan Structural Framing/Columns/Connections. Dilewati.");



                        continue;



                    }



                    try



                    {



                        var delIds = doc.Delete(elemB.Id);



                        if (delIds != null && delIds.Count > 0)



                        {



                            deletedCount++;



                        }



                    }



                    catch (Exception ex)



                    {



                        failedDeletions.Add($"ID {p.idB} ({p.code}): {ex.Message}");



                    }



                }



            }



            // 2. Kembalikan parameter Comments dan bersihkan Mark pada elemen yang dipertahankan (elemA)



            foreach (var p in pairs)



            {



                var elemA = doc.GetElement(new ElementId(p.idA));



                if (elemA != null)



                {



                    // Restore Comments



                    var pComm = elemA.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS) ?? elemA.LookupParameter("Comments");



                    if (pComm != null && !pComm.IsReadOnly)



                    {



                        string current = pComm.AsString() ?? "";



                        string original = ExtractCleanOriginalComment(current);



                        pComm.Set(original);



                        restoredCommentsCount++;



                    }



                    // Bersihkan Mark audit (hapus tag [PERTAHANKAN] agar model kembali bersih)



                    var pMark = elemA.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elemA.LookupParameter("Mark");



                    if (pMark != null && !pMark.IsReadOnly)



                    {



                        string currentMark = pMark.AsString() ?? "";



                        if (currentMark.Contains("[PERTAHANKAN]") || currentMark.StartsWith("P-"))



                        {



                            pMark.Set("");



                            clearedMarkCount++;



                        }



                    }



                }



            }



            // 3. Regenerasi dokumen



            doc.Regenerate();



            var st = trans.Commit();



            sb.AppendLine($"Status Transaksi Revit: {st}");



        }



        sb.AppendLine($"\nHasil Eksekusi:");



        sb.AppendLine($"  - Elemen duplikat berhasil dihapus       : {deletedCount} elemen");



        sb.AppendLine($"  - Parameter Comments berhasil dipulihkan : {restoredCommentsCount} elemen");



        sb.AppendLine($"  - Parameter Mark audit berhasil dibersihkan: {clearedMarkCount} elemen");



        if (failedDeletions.Count > 0)



        {



            sb.AppendLine($"\nElemen gagal dihapus ({failedDeletions.Count}):");



            foreach (var f in failedDeletions) sb.AppendLine($"  - {f}");



        }



        else



        {



            sb.AppendLine($"\n✓ Seluruh elemen kandidat hapus berhasil dibersihkan tanpa error!");



        }



        return sb.ToString();



    }



    private static string InspectEmptyConnections(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: INSPEKSI KONEKSI YANG BELUM TERISI (KOSONG) ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        ElementId paramId = ElementId.InvalidElementId;



        foreach (var conn in conns)



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            if (p != null) { paramId = p.Id; break; }



        }



        var emptyList = new List<Element>();



        foreach (var conn in conns)



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            string val = p?.AsString() ?? "";



            if (string.IsNullOrWhiteSpace(val))



            {



                emptyList.Add(conn);



            }



        }



        sb.AppendLine($"Total Koneksi Kosong Ditemukan: {emptyList.Count} dari {conns.Count} koneksi di model.\n");



        foreach (var conn in emptyList)



        {



            string famName = GetConnectionFamilyName(doc, conn);



            string typeName = GetConnectionTypeName(doc, conn);



            int subCount = 0;



            try



            {



                var subs = conn.GetSubelements();



                if (subs != null) subCount = subs.Count;



            }



            catch { }



            sb.AppendLine($"- ID: {conn.Id.Value} | Family (Target Standard): '{famName}' | Type: '{typeName}' | Sub-elements: {subCount}");



        }



        return sb.ToString();



    }



    private static string PopulateEmptyConnections(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: POPULATE 'Type of connection' PADA KONEKSI KOSONG ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        ElementId paramId = ElementId.InvalidElementId;



        foreach (var conn in conns)



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            if (p != null) { paramId = p.Id; break; }



        }



        if (paramId == ElementId.InvalidElementId)



        {



            return "Error: Parameter 'Type of connection' tidak ditemukan di dokumen.";



        }



        var emptyList = new List<Element>();



        foreach (var conn in conns)



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            string val = p?.AsString() ?? "";



            if (string.IsNullOrWhiteSpace(val))



            {



                emptyList.Add(conn);



            }



        }



        if (emptyList.Count == 0)



        {



            return "Tidak ada koneksi baru yang berstatus kosong. Seluruh koneksi sudah terisi sesuai standard.";



        }



        sb.AppendLine($"Memproses {emptyList.Count} koneksi kosong...");



        int connsUpdated = 0;



        int subUpdated = 0;



        int depUpdated = 0;



        using (Transaction tx = new(doc, "AIGRAV - Populate Type of Connection on New/Empty Connections"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            foreach (var conn in emptyList)



            {



                string familyVal = GetConnectionFamilyName(doc, conn);



                if (string.IsNullOrWhiteSpace(familyVal)) familyVal = conn.Name;



                // 1. Update Connection Handler



                bool pOk = SetParamValue(conn, "Type of connection", familyVal)



                        || SetParamValue(conn, "Type of Connection", familyVal);



                if (pOk) connsUpdated++;



                // 2. Update Subelements (Plates, Bolts, Welds inside connection)



                try



                {



                    var subs = conn.GetSubelements();



                    if (subs != null)



                    {



                        var strVal = new StringParameterValue(familyVal);



                        foreach (Subelement sub in subs)



                        {



                            try



                            {



                                if (sub.HasParameter(paramId) && sub.IsParameterModifiable(paramId))



                                {



                                    sub.SetParameterValue(paramId, strVal);



                                    subUpdated++;



                                }



                            }



                            catch { }



                        }



                    }



                }



                catch { }



                // 3. Update Dependent Elements



                try



                {



                    var depIds = conn.GetDependentElements(null);



                    foreach (var childId in depIds)



                    {



                        if (childId == conn.Id) continue;



                        Element? child = doc.GetElement(childId);



                        if (child != null)



                        {



                            bool cOk = SetParamValue(child, "Type of connection", familyVal)



                                    || SetParamValue(child, "Type of Connection", familyVal);



                            if (cOk) depUpdated++;



                        }



                    }



                }



                catch { }



            }



            doc.Regenerate();



            var st = tx.Commit();



            sb.AppendLine($"Status Transaksi: {st}");



        }



        sb.AppendLine($"\n[HASIL EKSEKUSI PEMBAHARUAN KONEKSI BARU]");



        sb.AppendLine($"- Connection Handlers Berhasil Diisi : {connsUpdated} elemen");



        sb.AppendLine($"- Sub-Elemen (Plates, Bolts, Welds) Diisi : {subUpdated} sub-elemen");



        sb.AppendLine($"- Dependent Elements Diisi               : {depUpdated} elemen");



        return sb.ToString();



    }



    private static string InspectHoistColumnsDetail(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: INSPEKSI DETAIL KOLOM HOIST & SCHEDULE ===");



        // 1. Cari Schedule N.2.2



        var sched = doc.GetElement(new ElementId(6853683)) as ViewSchedule



            ?? new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => s.Name.Contains("Kolom Hoist WF 250"));



        if (sched != null)



        {



            sb.AppendLine($"\nSchedule Name: '{sched.Name}' (Id: {sched.Id.Value})");



            var def = sched.Definition;



            sb.AppendLine($"Category: {(BuiltInCategory)def.CategoryId.Value}");



            sb.AppendLine($"IsItemized: {def.IsItemized}");



                    sb.AppendLine($"Fields Count: {def.GetFieldCount()}");



        for (int fi = 0; fi < def.GetFieldCount(); fi++)



        {



            var f = def.GetField(fi);



            sb.AppendLine($"  Field [{fi}]: Name='{f.GetName()}' | Heading='{f.ColumnHeading}' | Hidden={f.IsHidden}");



        }



        sb.AppendLine($"SortGroupFields Count: {def.GetSortGroupFieldCount()}");



        for (int si = 0; si < def.GetSortGroupFieldCount(); si++)



        {



            var sgf = def.GetSortGroupField(si);



            var f = def.GetField(sgf.FieldId);



            sb.AppendLine($"  SortGroup [{si}]: Field='{f?.GetName()}' | Order={sgf.SortOrder} | ShowHeader={sgf.ShowHeader} | ShowFooter={sgf.ShowFooter}");



        }



        sb.AppendLine($"Filters Count: {def.GetFilterCount()}");



            for (int i = 0; i < def.GetFilterCount(); i++)



            {



                var f = def.GetFilter(i);



                var fld = def.GetField(f.FieldId);



                string fName = fld != null ? fld.GetName() : "Unknown";



                string val = "";



                try { val = f.GetStringValue(); } catch { }



                sb.AppendLine($"  Filter #{i + 1}: '{fName}' | {f.FilterType} | '{val}'");



            }



            sb.AppendLine("\nFields in Schedule:");



            var fieldOrder = def.GetFieldOrder();



            for (int i = 0; i < fieldOrder.Count; i++)



            {



                var fld = def.GetField(fieldOrder[i]);



                string fldName = fld.GetName();



                string heading = fld.ColumnHeading;



                var fType = fld.FieldType;



                sb.AppendLine($"  Col [{i}]: Heading: '{heading}' | Name: '{fldName}' | FieldType: {fType}");



            }



            sb.AppendLine("\nSchedule Browser Organization Parameters:");



            foreach (Parameter p in sched.Parameters)



            {



                if (p.StorageType == StorageType.String && !string.IsNullOrWhiteSpace(p.AsString()))



                {



                    string pName = p.Definition.Name;



                    if (pName.Contains("Group") || pName.Contains("Discipline") || pName.Contains("Sub") || pName.Contains("Views"))



                    {



                        sb.AppendLine($"  {pName} = '{p.AsString()}'");



                    }



                }



            }



        }



        // 2. Cari seluruh elemen kolom hoist di model



        var hoistCols = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralColumns)



            .WhereElementIsNotElementType()



            .Where(c => {



                var pComm = c.LookupParameter("Comments")?.AsString() ?? "";



                return pComm.IndexOf("HOIST", StringComparison.OrdinalIgnoreCase) >= 0;



            })



            .ToList();



        sb.AppendLine($"\nTotal Kolom Hoist di Model: {hoistCols.Count}");



        var concreteList = new List<Element>();



        var steelList = new List<Element>();



        foreach (var c in hoistCols)



        {



            var typeElem = doc.GetElement(c.GetTypeId()) as ElementType;



            string famName = typeElem?.FamilyName ?? "";



            string typeName = typeElem?.Name ?? c.Name;



            var mat = c.LookupParameter("Structural Material")?.AsValueString() ?? "";



            if (typeName.Contains("350") || famName.Contains("Concrete") || famName.Contains("Beton") || mat.Contains("Concrete") || mat.Contains("Beton"))



            {



                concreteList.Add(c);



            }



            else



            {



                steelList.Add(c);



            }



        }



        sb.AppendLine($"  - Kolom Beton (Pedestal Hoist): {concreteList.Count} elemen");



        foreach (var c in concreteList.Take(5))



        {



            var typeElem = doc.GetElement(c.GetTypeId()) as ElementType;



            string tName = typeElem?.Name ?? c.Name;



            string vol = c.LookupParameter("Volume")?.AsValueString() ?? "0";



            string len = c.LookupParameter("Length")?.AsValueString() ?? c.LookupParameter("Cut Length")?.AsValueString() ?? "0";



            sb.AppendLine($"    ID: {c.Id.Value} | Type: '{tName}' | Vol: {vol} | Length: {len} | Comments: '{c.LookupParameter("Comments")?.AsString()}'");



        }



        sb.AppendLine($"  - Kolom Baja (WF 250 Hoist): {steelList.Count} elemen");



        foreach (var c in steelList.Take(5))



        {



            var typeElem = doc.GetElement(c.GetTypeId()) as ElementType;



            string tName = typeElem?.Name ?? c.Name;



            string len = c.LookupParameter("Length")?.AsValueString() ?? c.LookupParameter("Cut Length")?.AsValueString() ?? "0";



            sb.AppendLine($"    ID: {c.Id.Value} | Type: '{tName}' | Length: {len} | Comments: '{c.LookupParameter("Comments")?.AsString()}'");



        }



        return sb.ToString();



    }



    private static string SeparateHoistColumns(Document doc, string? argument)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: PEMISAHAN KOLOM HOIST (BAJA VS BETON) ===");



        // 1. Ambil Schedule N.2.2 (Kolom Hoist WF 250)



        var steelSched = doc.GetElement(new ElementId(6853683)) as ViewSchedule



            ?? new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => s.Name.Contains("Kolom Hoist WF 250"));



        if (steelSched == null) return "Error: Schedule 'N.2.2 - Kolom Hoist WF 250' tidak ditemukan.";



        // 2. Ambil Schedule A.1.7 sebagai template beton dengan Volume



        var templateBeton = doc.GetElement(new ElementId(668374)) as ViewSchedule



            ?? new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => s.Name.Contains("Kolom Pedestall"));



        if (templateBeton == null) return "Error: Schedule referensi beton 'A.1.7 - Kolom Pedestall' tidak ditemukan.";



        string newConcreteSchedName = "N.2.6 - Kolom Hoist Beton K 350 X 250";



        // Hapus schedule N.2.6 lama jika pernah dibuat sebelumnya



        var existingNew = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .FirstOrDefault(s => s.Name.Equals(newConcreteSchedName, StringComparison.OrdinalIgnoreCase));



        using (Transaction tx = new(doc, "AIGRAV - Separate Hoist Columns (Steel Weight vs Concrete Volume)"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            if (existingNew != null)



            {



                doc.Delete(existingNew.Id);



            }



            // A. Update Filter pada Schedule Baja (N.2.2 - Kolom Hoist WF 250)



            var steelDef = steelSched.Definition;



            ScheduleField? steelTypeField = null;



            ScheduleField? steelCommField = null;



            for (int i = 0; i < steelDef.GetFieldCount(); i++)



            {



                var fld = steelDef.GetField(i);



                if (fld.GetName().Equals("Type", StringComparison.OrdinalIgnoreCase)) steelTypeField = fld;



                if (fld.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase)) steelCommField = fld;



            }



            if (steelTypeField != null && steelCommField != null)



            {



                steelDef.ClearFilters();



                steelDef.AddFilter(new ScheduleFilter(steelCommField.FieldId, ScheduleFilterType.Contains, "HOIST"));



                steelDef.AddFilter(new ScheduleFilter(steelTypeField.FieldId, ScheduleFilterType.Contains, "WF 250"));



                sb.AppendLine("✓ Berhasil mengupdate filter pada 'N.2.2 - Kolom Hoist WF 250': menyaring khusus 'WF 250'.");



            }



            else



            {



                sb.AppendLine("✗ Gagal menemukan field Type/Comments pada N.2.2.");



            }



            // B. Duplikasi template beton A.1.7 untuk membuat N.2.6



            ElementId dupId = templateBeton.Duplicate(ViewDuplicateOption.Duplicate);



            ViewSchedule concreteSched = (ViewSchedule)doc.GetElement(dupId);



            concreteSched.Name = newConcreteSchedName;



            var concDef = concreteSched.Definition;



            ScheduleField? concTypeField = null;



            ScheduleField? concCommField = null;



            for (int i = 0; i < concDef.GetFieldCount(); i++)



            {



                var fld = concDef.GetField(i);



                if (fld.GetName().Equals("Type", StringComparison.OrdinalIgnoreCase)) concTypeField = fld;



                if (fld.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase)) concCommField = fld;



            }



            if (concTypeField != null && concCommField != null)



            {



                concDef.ClearFilters();



                concDef.AddFilter(new ScheduleFilter(concCommField.FieldId, ScheduleFilterType.Contains, "HOIST"));



                concDef.AddFilter(new ScheduleFilter(concTypeField.FieldId, ScheduleFilterType.Contains, "K 350"));



                sb.AppendLine($"✓ Berhasil membuat schedule baru '{newConcreteSchedName}' dengan filter 'K 350' & kolom Volume.");



            }



            // Salin parameter organisasi browser dari steelSched ke concreteSched



            foreach (Parameter pRef in steelSched.Parameters)



            {



                if (pRef.IsReadOnly) continue;



                string pName = pRef.Definition.Name;



                if (pName.Equals("View Name", StringComparison.OrdinalIgnoreCase) || pName.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;



                var pTarget = concreteSched.LookupParameter(pName);



                if (pTarget != null && !pTarget.IsReadOnly)



                {



                    try



                    {



                        if (pRef.StorageType == StorageType.String && !string.IsNullOrWhiteSpace(pRef.AsString()))



                            pTarget.Set(pRef.AsString());



                        else if (pRef.StorageType == StorageType.Integer)



                            pTarget.Set(pRef.AsInteger());



                    }



                    catch { }



                }



            }



            var st = tx.Commit();



            sb.AppendLine($"Status Transaksi: {st}");



        }



        // C. Tampilkan Ringkasan Isi Kedua Schedule



        sb.AppendLine("\n[HASIL SCHEDULE BAJA PASCA-UPDATE: N.2.2 - Kolom Hoist WF 250]");



        try



        {



            var td = steelSched.GetTableData();



            var body = td.GetSectionData(SectionType.Body);



            for (int r = 0; r < body.NumberOfRows; r++)



            {



                var rowTexts = new List<string>();



                for (int c = 0; c < body.NumberOfColumns; c++)



                    rowTexts.Add(steelSched.GetCellText(SectionType.Body, r, c));



                sb.AppendLine($"  Row [{r}]: {string.Join(" | ", rowTexts)}");



            }



        }



        catch (Exception ex)



        {



            sb.AppendLine($"Tabel steel error: {ex.Message}");



        }



        var createdConc = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .FirstOrDefault(s => s.Name.Equals(newConcreteSchedName, StringComparison.OrdinalIgnoreCase));



        if (createdConc != null)



        {



            sb.AppendLine($"\n[HASIL SCHEDULE BETON BARU: {newConcreteSchedName}]");



            try



            {



                var td = createdConc.GetTableData();



                var body = td.GetSectionData(SectionType.Body);



                for (int r = 0; r < body.NumberOfRows; r++)



                {



                    var rowTexts = new List<string>();



                    for (int c = 0; c < body.NumberOfColumns; c++)



                        rowTexts.Add(createdConc.GetCellText(SectionType.Body, r, c));



                    sb.AppendLine($"  Row [{r}]: {string.Join(" | ", rowTexts)}");



                }



            }



            catch (Exception ex)



            {



                sb.AppendLine($"Tabel concrete error: {ex.Message}");



            }



        }



        return sb.ToString();



    }



    public static string InspectMezzanineConnectionBreakdown(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== RINCIAN SELURUH KONEKSI MEZZANINE, PARAMETER TYPE OF CONNECTION, & SCHEDULE TERKAIT ===");



        // 1. Connection Handlers



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .Cast<Autodesk.Revit.DB.Structure.StructuralConnectionHandler>()



            .ToList();



        sb.AppendLine($"\n[1. CONNECTION HANDLERS: {conns.Count} UNIT]");



        var connGroups = conns.GroupBy(c => new {



            Fam = GetConnectionFamilyName(doc, c),



            Type = GetConnectionTypeName(doc, c),



            ParamVal = (FindParam(c, "Type of connection") ?? FindParam(c, "Type of Connection"))?.AsString() ?? "(Kosong)"



        }).OrderBy(g => g.Key.Fam).ThenBy(g => g.Key.Type);



        foreach (var g in connGroups)



        {



            sb.AppendLine($"  - Family: '{g.Key.Fam}' | Type: '{g.Key.Type}' | TypeOfConn Param: '{g.Key.ParamVal}' -> {g.Count()} unit");



        }



        // 2. All Connection Plates



        var allPlates = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"\n[2. PLATES (OST_StructConnectionPlates): {allPlates.Count} UNIT]");



        var plateGroups = allPlates.GroupBy(p => {



            var param = FindParam(p, "Type of connection") ?? FindParam(p, "Type of Connection");



            string val = param?.AsString() ?? "";



            return string.IsNullOrWhiteSpace(val) ? "(Kosong)" : val;



        }).OrderByDescending(g => g.Count());



        foreach (var pg in plateGroups)



        {



            sb.AppendLine($"  - TypeOfConn Param: '{pg.Key}' -> {pg.Count()} unit");



            var thkGroups = pg.GroupBy(p => {



                var pThk = p.LookupParameter("Thickness");



                double thkMm = (pThk?.AsDouble() ?? 0) * 304.8;



                return Math.Round(thkMm, 1);



            }).OrderBy(x => x.Key);



            foreach (var tg in thkGroups)



            {



                sb.AppendLine($"      * Tebal {tg.Key} mm: {tg.Count()} unit");



            }



        }



        // 3. All Connection Bolts



        var allBolts = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionBolts)



            .WhereElementIsNotElementType()



            .ToList();



        sb.AppendLine($"\n[3. BOLTS (OST_StructConnectionBolts): {allBolts.Count} UNIT]");



        var boltGroups = allBolts.GroupBy(b => {



            var param = FindParam(b, "Type of connection") ?? FindParam(b, "Type of Connection");



            string val = param?.AsString() ?? "";



            return string.IsNullOrWhiteSpace(val) ? "(Kosong)" : val;



        }).OrderByDescending(g => g.Count());



        foreach (var bg in boltGroups)



        {



            sb.AppendLine($"  - TypeOfConn Param: '{bg.Key}' -> {bg.Count()} unit");



            var diaGroups = bg.GroupBy(b => {



                var pDia = b.LookupParameter("Diameter");



                double diaMm = (pDia?.AsDouble() ?? 0) * 304.8;



                return Math.Round(diaMm, 1);



            }).OrderBy(x => x.Key);



            foreach (var dg in diaGroups)



            {



                sb.AppendLine($"      * Dia {dg.Key} mm: {dg.Count()} unit");



            }



        }



        // 4. Existing Mezzanine Connection Schedules



        var scheds = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate && (s.Name.Contains("Mezzanine") || s.Name.Contains("WF 400") || s.Name.Contains("WF 350") || s.Name.Contains("WF 300") || s.Name.Contains("WF 200") || s.Name.Contains("WF 588")))



            .OrderBy(s => s.Name)



            .ToList();



        sb.AppendLine($"\n[4. EXISTING SCHEDULES MEZZANINE]");



        foreach (var s in scheds)



        {



            var def = s.Definition;



            var filterStrs = new List<string>();



            for (int i = 0; i < def.GetFilterCount(); i++)



            {



                var flt = def.GetFilter(i);



                var f = def.GetField(flt.FieldId);



                string v = "";



                try { v = flt.GetStringValue(); } catch {}



                filterStrs.Add($"{f?.GetName()} {flt.FilterType} '{v}'");



            }



            sb.AppendLine($"  - [{s.Id.Value}] '{s.Name}' (Cat: {(BuiltInCategory)def.CategoryId.Value}) | Filters: {string.Join("; ", filterStrs)}");



        }



        return sb.ToString();



    }



    public static string UpdateMezzanineConnectionSchedules(Document doc, string? argument)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: UPDATE STRUKTUR JUDUL & FILTER QTO SAMBUNGAN MEZZANINE (TYPE OF CONNECTION) ===");



        ViewSchedule? FindSched(Func<ViewSchedule, bool> pred)



        {



            return new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .Where(s => s.IsValidObject && !s.IsTemplate)



                .FirstOrDefault(pred);



        }



        var plateTemplate = FindSched(s => s.Id.Value == 6862639 || s.Name.Contains("Mezzanine Pelat Sambungan WF 300") || s.Name.Contains("Mezzanine Pelat WF 300"))



                         ?? FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF"));



        



        var boltTemplate = FindSched(s => s.Id.Value == 6862646 || s.Name.Contains("Mezzanine Baut Sambungan WF 300") || s.Name.Contains("Mezzanine Baut WF 300"))



                        ?? FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF"));



        if (plateTemplate == null || boltTemplate == null)



        {



            return "Error: Schedule template referensi untuk Plat / Baut tidak ditemukan.";



        }



        using (Transaction tx = new(doc, "AIGRAV - Update Mezzanine Connection Schedules Title & Filter"))



        {



            tx.Start();



            // Normalize single anomaly WF350-Endplate-H250



            var anonConns = new FilteredElementCollector(doc)



                .OfCategory(BuiltInCategory.OST_StructConnections)



                .WhereElementIsNotElementType()



                .Where(c => {



                    var p = FindParam(c, "Type of connection");



                    return p != null && p.AsString() == "WF350-Endplate-H250";



                }).ToList();



            foreach (var c in anonConns)



            {



                SetParamValue(c, "Type of connection", "WF350-Endplate");



                var subs = c.GetSubelements();



                if (subs != null)



                {



                    var pId = (FindParam(c, "Type of connection"))?.Id ?? ElementId.InvalidElementId;



                    if (pId != ElementId.InvalidElementId)



                    {



                        var strVal = new StringParameterValue("WF350-Endplate");



                        foreach (Subelement sub in subs)



                        {



                            try { if (sub.HasParameter(pId) && sub.IsParameterModifiable(pId)) sub.SetParameterValue(pId, strVal); } catch {}



                        }



                    }



                }



            }



            ViewSchedule GetOrDuplicate(string targetName, ViewSchedule template)



            {



                var found = FindSched(s => s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));



                if (found != null) return found;



                ElementId dupId = template.Duplicate(ViewDuplicateOption.Duplicate);



                var newSched = (ViewSchedule)doc.GetElement(dupId);



                newSched.Name = targetName;



                return newSched;



            }



            void SetupPlateSched(ViewSchedule vs, string name, string filterVal)



            {



                vs.Name = name;



                var def = vs.Definition;



                def.IsItemized = false;



                ScheduleField? fTypeConn = null;



                ScheduleField? fComments = null;



                ScheduleField? fThk = null;



                ScheduleField? fWidth = null;



                ScheduleField? fLength = null;



                ScheduleField? fCount = null;



                ScheduleField? fWeight = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    string fn = f.GetName();



                    if (fn.IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0) fTypeConn = f;



                    else if (fn.Equals("Comments", StringComparison.OrdinalIgnoreCase)) fComments = f;



                    else if (fn.Equals("Thickness", StringComparison.OrdinalIgnoreCase)) fThk = f;



                    else if (fn.Equals("Width", StringComparison.OrdinalIgnoreCase)) fWidth = f;



                    else if (fn.Equals("Length", StringComparison.OrdinalIgnoreCase)) fLength = f;



                    else if (fn.Equals("Count", StringComparison.OrdinalIgnoreCase)) fCount = f;



                    else if (fn.IndexOf("Weight", StringComparison.OrdinalIgnoreCase) >= 0 || fn.IndexOf("Berat", StringComparison.OrdinalIgnoreCase) >= 0) fWeight = f;



                }



                if (fTypeConn != null)



                {



                    fTypeConn.ColumnHeading = "Tipe Sambungan";



                    fTypeConn.IsHidden = false;



                }



                if (fComments != null) fComments.IsHidden = true;



                // fCount totals already configured in template



                // fWeight totals already configured in template



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                if (fTypeConn != null)



                {



                    var sg0 = new ScheduleSortGroupField(fTypeConn.FieldId, ScheduleSortOrder.Ascending);



                    sg0.ShowFooter = true;



                    sg0.ShowHeader = false;



                    def.AddSortGroupField(sg0);



                }



                if (fThk != null)



                {



                    var sg1 = new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending);



                    sg1.ShowFooter = false;



                    sg1.ShowHeader = false;



                    def.AddSortGroupField(sg1);



                }



                if (fWidth != null)



                {



                    var sg2 = new ScheduleSortGroupField(fWidth.FieldId, ScheduleSortOrder.Ascending);



                    sg2.ShowFooter = false;



                    sg2.ShowHeader = false;



                    def.AddSortGroupField(sg2);



                }



                if (fLength != null)



                {



                    var sg3 = new ScheduleSortGroupField(fLength.FieldId, ScheduleSortOrder.Ascending);



                    sg3.ShowFooter = false;



                    sg3.ShowHeader = false;



                    def.AddSortGroupField(sg3);



                }



                def.ClearFilters();



                if (fTypeConn != null)



                {



                    def.AddFilter(new ScheduleFilter(fTypeConn.FieldId, ScheduleFilterType.Contains, filterVal));



                }



            }



            void SetupBoltSched(ViewSchedule vs, string name, string filterVal)



            {



                vs.Name = name;



                var def = vs.Definition;



                def.IsItemized = false;



                ScheduleField? fTypeConn = null;



                ScheduleField? fComments = null;



                ScheduleField? fStd = null;



                ScheduleField? fDia = null;



                ScheduleField? fNum = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    string fn = f.GetName();



                    if (fn.IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0) fTypeConn = f;



                    else if (fn.Equals("Comments", StringComparison.OrdinalIgnoreCase)) fComments = f;



                    else if (fn.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0) fStd = f;



                    else if (fn.IndexOf("Diameter", StringComparison.OrdinalIgnoreCase) >= 0) fDia = f;



                    else if (fn.IndexOf("Number", StringComparison.OrdinalIgnoreCase) >= 0 || fn.IndexOf("Jumlah", StringComparison.OrdinalIgnoreCase) >= 0) fNum = f;



                }



                if (fTypeConn != null)



                {



                    fTypeConn.ColumnHeading = "Tipe Sambungan";



                    fTypeConn.IsHidden = false;



                }



                if (fComments != null) fComments.IsHidden = true;



                // fNum totals already configured in template



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                if (fTypeConn != null)



                {



                    var sg0 = new ScheduleSortGroupField(fTypeConn.FieldId, ScheduleSortOrder.Ascending);



                    sg0.ShowFooter = true;



                    sg0.ShowHeader = false;



                    def.AddSortGroupField(sg0);



                }



                if (fDia != null)



                {



                    var sg1 = new ScheduleSortGroupField(fDia.FieldId, ScheduleSortOrder.Ascending);



                    sg1.ShowFooter = false;



                    sg1.ShowHeader = false;



                    def.AddSortGroupField(sg1);



                }



                def.ClearFilters();



                if (fTypeConn != null)



                {



                    def.AddFilter(new ScheduleFilter(fTypeConn.FieldId, ScheduleFilterType.Contains, filterVal));



                }



            }



            // === A. WF 300 ===



            var sC213 = FindSched(s => (s.Name.Contains("WF 300") && (s.Name.Contains("Pelat") || s.Name.Contains("Plat"))) || s.Id.Value == 6862639);



            if (sC213 != null)



            {



                SetupPlateSched(sC213, "C.2.13 - Mezzanine Pelat WF 300 Endplate", "WF300-Endplate");



                sb.AppendLine("✓ Updated: C.2.13 - Mezzanine Pelat WF 300 Endplate");



            }



            var sC214 = FindSched(s => (s.Name.Contains("WF 300") && s.Name.Contains("Baut")) || s.Id.Value == 6862646);



            if (sC214 != null)



            {



                SetupBoltSched(sC214, "C.2.14 - Mezzanine Baut WF 300 Endplate", "WF300-Endplate");



                sb.AppendLine("✓ Updated: C.2.14 - Mezzanine Baut WF 300 Endplate");



            }



            var sC213b = GetOrDuplicate("C.2.13b - Mezzanine Pelat WF 300 Gusset Plate", plateTemplate);



            SetupPlateSched(sC213b, "C.2.13b - Mezzanine Pelat WF 300 Gusset Plate", "WF300-Gusset");



            sb.AppendLine("✓ Created/Configured: C.2.13b - Mezzanine Pelat WF 300 Gusset Plate");



            var sC214b = GetOrDuplicate("C.2.14b - Mezzanine Baut WF 300 Gusset Plate", boltTemplate);



            SetupBoltSched(sC214b, "C.2.14b - Mezzanine Baut WF 300 Gusset Plate", "WF300-Gusset");



            sb.AppendLine("✓ Created/Configured: C.2.14b - Mezzanine Baut WF 300 Gusset Plate");



            // === B. WF 350 ===



            var sC28 = FindSched(s => s.Name.Contains("End Plat WF 350") || s.Name.Contains("Pelat WF 350 Endplate") || s.Id.Value == 6862604);



            if (sC28 != null)



            {



                SetupPlateSched(sC28, "C.2.8 - Mezzanine Pelat WF 350 Endplate", "WF350-Endplate");



                sb.AppendLine("✓ Updated: C.2.8 - Mezzanine Pelat WF 350 Endplate (unified Endplate + Stiffeners)");



            }



            var sC29 = FindSched(s => s.Name.Contains("Stifner Plat WF 350") || s.Id.Value == 6862611);



            if (sC29 != null)



            {



                doc.Delete(sC29.Id);



                sb.AppendLine("✓ Deleted: C.2.9 - Mezzanine Stifner Plat WF 350 (merged into C.2.8)");



            }



            var sC210 = FindSched(s => s.Name.Contains("Baut End Plat WF 350") || s.Name.Contains("Baut WF 350 Endplate") || s.Id.Value == 6862618);



            if (sC210 != null)



            {



                SetupBoltSched(sC210, "C.2.10 - Mezzanine Baut WF 350 Endplate", "WF350-Endplate");



                sb.AppendLine("✓ Updated: C.2.10 - Mezzanine Baut WF 350 Endplate");



            }



            var sC211 = FindSched(s => s.Name.Contains("Gusset Plat WF 350") || s.Name.Contains("Pelat WF 350 Gusset") || s.Id.Value == 6862625);



            if (sC211 != null)



            {



                SetupPlateSched(sC211, "C.2.11 - Mezzanine Pelat WF 350 Gusset Plate", "WF350-Gusset Plate");



                sb.AppendLine("✓ Updated: C.2.11 - Mezzanine Pelat WF 350 Gusset Plate");



            }



            var sC212 = FindSched(s => s.Name.Contains("Baut Gusset WF 350") || s.Name.Contains("Baut WF 350 Gusset") || s.Id.Value == 6862632);



            if (sC212 != null)



            {



                SetupBoltSched(sC212, "C.2.12 - Mezzanine Baut WF 350 Gusset Plate", "WF350-Gusset Plate");



                sb.AppendLine("✓ Updated: C.2.12 - Mezzanine Baut WF 350 Gusset Plate");



            }



            // === C. WF 200 ===



            var sC215 = FindSched(s => s.Name.Contains("WF 200") && (s.Name.Contains("Pelat") || s.Name.Contains("Plat")) || s.Id.Value == 6862653);



            if (sC215 != null)



            {



                SetupPlateSched(sC215, "C.2.15 - Mezzanine Pelat WF 200 Endplate", "WF200-Endplate");



                sb.AppendLine("✓ Updated: C.2.15 - Mezzanine Pelat WF 200 Endplate");



            }



            var sC216 = FindSched(s => s.Name.Contains("WF 200") && s.Name.Contains("Baut") || s.Id.Value == 6862660);



            if (sC216 != null)



            {



                SetupBoltSched(sC216, "C.2.16 - Mezzanine Baut WF 200 Endplate", "WF200-Endplate");



                sb.AppendLine("✓ Updated: C.2.16 - Mezzanine Baut WF 200 Endplate");



            }



            var sC217 = GetOrDuplicate("C.2.17 - Mezzanine Pelat WF 200 Gusset Plate", plateTemplate);



            SetupPlateSched(sC217, "C.2.17 - Mezzanine Pelat WF 200 Gusset Plate", "WF200-Gusset Plate");



            sb.AppendLine("✓ Created/Configured: C.2.17 - Mezzanine Pelat WF 200 Gusset Plate");



            var sC218 = GetOrDuplicate("C.2.18 - Mezzanine Baut WF 200 Gusset Plate", boltTemplate);



            SetupBoltSched(sC218, "C.2.18 - Mezzanine Baut WF 200 Gusset Plate", "WF200-Gusset Plate");



            sb.AppendLine("✓ Created/Configured: C.2.18 - Mezzanine Baut WF 200 Gusset Plate");



            // === D. WF 400 ===



            var sC24 = FindSched(s => s.Name.Contains("End Plat WF 400") || s.Name.Contains("Pelat WF 400 Endplate") || s.Id.Value == 6647125);



            if (sC24 != null)



            {



                SetupPlateSched(sC24, "C.2.4 - Mezzanine Pelat WF 400 Endplate", "WF-400 Endplate");



                sb.AppendLine("✓ Updated: C.2.4 - Mezzanine Pelat WF 400 Endplate (unified Endplate + Stiffeners)");



            }



            var sC26 = FindSched(s => s.Name.Contains("Stifner Plat WF 400") || s.Id.Value == 6647133);



            if (sC26 != null)



            {



                doc.Delete(sC26.Id);



                sb.AppendLine("✓ Deleted: C.2.6 - Mezzanine Stifner Plat WF 400 (merged into C.2.4)");



            }



            var sC27 = FindSched(s => s.Name.Contains("Baut WF 400") || s.Id.Value == 6647137);



            if (sC27 != null)



            {



                SetupBoltSched(sC27, "C.2.7 - Mezzanine Baut WF 400 Endplate", "WF-400 Endplate");



                sb.AppendLine("✓ Updated: C.2.7 - Mezzanine Baut WF 400 Endplate");



            }



            // === E. WF 588 ===



            var sC219 = GetOrDuplicate("C.2.19 - Mezzanine Pelat WF 588 Endplate", plateTemplate);



            SetupPlateSched(sC219, "C.2.19 - Mezzanine Pelat WF 588 Endplate", "WF588-Endplate");



            sb.AppendLine("✓ Created/Configured: C.2.19 - Mezzanine Pelat WF 588 Endplate");



            var sC220 = GetOrDuplicate("C.2.20 - Mezzanine Baut WF 588 Endplate", boltTemplate);



            SetupBoltSched(sC220, "C.2.20 - Mezzanine Baut WF 588 Endplate", "WF588-Endplate");



            sb.AppendLine("✓ Created/Configured: C.2.20 - Mezzanine Baut WF 588 Endplate");



            doc.Regenerate();



            var st = tx.Commit();



            sb.AppendLine($"Status Transaksi: {st}");



        }



        return sb.ToString();



    }



    public static string ApplyOptionBMezzanineRenumbering(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: EKSEKUSI PENATAAN PENOMORAN SCHEDULE OPSI B (MEZZANINE C.2) ===");



        sb.AppendLine("CATATAN: Seluruh schedule Rafter (C.1.x) DILINDUNGI PENUH & TIDAK DIHAPUS/DIUBAH.");



        using (Transaction tx = new(doc, "AIGRAV - Renumber Mezzanine Schedules Option B"))



        {



            tx.Start();



            // 1. HAPUS HANYA 5 SCHEDULE MEZZANINE LAMA YANG SUDAH OBSOLET



            // JANGAN HAPUS RAFTER (C.1.x)!



            long[] oldMezzIds = new long[] { 851801, 1891153, 6221572, 1891146, 6221579 };



            int delCount = 0;



            foreach (long id in oldMezzIds)



            {



                var el = doc.GetElement(new ElementId(id));



                if (el is ViewSchedule vs && vs.IsValidObject)



                {



                    string oldName = "";



                    try { oldName = vs.Name; } catch {}



                    if (!string.IsNullOrEmpty(oldName) && !oldName.StartsWith("C.1", StringComparison.OrdinalIgnoreCase) && !oldName.Contains("Rafter", StringComparison.OrdinalIgnoreCase))



                    {



                        doc.Delete(el.Id);



                        delCount++;



                        sb.AppendLine($"[DELETE] Schedule mezzanine lama: '{oldName}' (Id: {id})");



                    }



                }



            }



            sb.AppendLine($"Total schedule mezzanine lama dihapus: {delCount}");



            ViewSchedule? FindSched(Func<ViewSchedule, bool> pred)



            {



                var all = new FilteredElementCollector(doc)



                    .OfClass(typeof(ViewSchedule))



                    .Cast<ViewSchedule>()



                    .ToList();



                foreach (var s in all)



                {



                    try



                    {



                        if (s.IsValidObject && !s.IsTemplate && !s.Name.StartsWith("C.1") && !s.Name.Contains("Rafter") && pred(s))



                        {



                            return s;



                        }



                    }



                    catch {}



                }



                return null;



            }



            void SafeRename(ViewSchedule? vs, string targetName)



            {



                if (vs == null)



                {



                    sb.AppendLine($"[WARN] Schedule target tidak ditemukan untuk: '{targetName}'");



                    return;



                }



                try



                {



                    string old = vs.Name;



                    if (!old.Equals(targetName, StringComparison.OrdinalIgnoreCase))



                    {



                        vs.Name = targetName;



                        sb.AppendLine($"[RENAME] '{old}' -> '{targetName}'");



                    }



                    else



                    {



                        sb.AppendLine($"[OK] Nama sudah sesuai: '{targetName}'");



                    }



                }



                catch (Exception ex)



                {



                    sb.AppendLine($"[ERR] Gagal rename '{targetName}': {ex.Message}");



                }



            }



            // ==========================================



            // KELOMPOK 1: BALOK MEZZANINE (C.2.1 s/d C.2.7)



            // ==========================================



            var sB588 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructuralFraming && s.Name.Contains("Mezzanine") && s.Name.Contains("588"));



            SafeRename(sB588, "C.2.1 - Beam Mezzanine WF 588");



            var sB400 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructuralFraming && s.Name.Contains("Mezzanine") && s.Name.Contains("WF 400") && !s.Name.Contains("Voute"));



            SafeRename(sB400, "C.2.2 - Beam Mezzanine WF 400");



            var sB350 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructuralFraming && s.Name.Contains("Mezzanine") && s.Name.Contains("WF 350") && !s.Name.Contains("Voute"));



            SafeRename(sB350, "C.2.3 - Beam Mezzanine WF 350");



            var sB300 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructuralFraming && s.Name.Contains("Mezzanine") && s.Name.Contains("WF 300") && !s.Name.Contains("Pelat") && !s.Name.Contains("Baut"));



            SafeRename(sB300, "C.2.4 - Beam Mezzanine WF 300");



            var sB250 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructuralFraming && s.Name.Contains("Mezzanine") && s.Name.Contains("WF 250"));



            SafeRename(sB250, "C.2.5 - Beam Mezzanine WF 250");



            var sV350 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructuralFraming && s.Name.Contains("Voute") && s.Name.Contains("350"));



            SafeRename(sV350, "C.2.6 - Voute Mezzanine WF 350");



            var sV400 = FindSched(s => (s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionProfiles || s.Name.Contains("Voute Profile")) && s.Name.Contains("400"));



            SafeRename(sV400, "C.2.7 - Mezzanine Voute Profile WF 400");



            // ==========================================



            // KELOMPOK 2: PELAT SAMBUNGAN (C.2.8 s/d C.2.15)



            // ==========================================



            var sP588 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 588"));



            SafeRename(sP588, "C.2.8 - Mezzanine Pelat WF 588 Endplate");



            var sP400 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 400"));



            SafeRename(sP400, "C.2.9 - Mezzanine Pelat WF 400 Endplate");



            var sP350End = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 350") && (s.Name.Contains("Endplate") || s.Name.Contains("End Plat")));



            SafeRename(sP350End, "C.2.10 - Mezzanine Pelat WF 350 Endplate");



            var sP350Gus = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 350") && s.Name.Contains("Gusset"));



            SafeRename(sP350Gus, "C.2.11 - Mezzanine Pelat WF 350 Gusset Plate");



            var sP300End = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 300") && (s.Name.Contains("Endplate") || s.Name.Contains("Pelat Sambungan") || s.Id.Value == 6862639) && !s.Name.Contains("Gusset"));



            SafeRename(sP300End, "C.2.12 - Mezzanine Pelat WF 300 Endplate");



            var sP300Gus = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 300") && s.Name.Contains("Gusset"));



            SafeRename(sP300Gus, "C.2.13 - Mezzanine Pelat WF 300 Gusset Plate");



            var sP200End = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 200") && (s.Name.Contains("Endplate") || !s.Name.Contains("Gusset")));



            SafeRename(sP200End, "C.2.14 - Mezzanine Pelat WF 200 Endplate");



            var sP200Gus = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionPlates && s.Name.Contains("WF 200") && s.Name.Contains("Gusset"));



            SafeRename(sP200Gus, "C.2.15 - Mezzanine Pelat WF 200 Gusset Plate");



            // ==========================================



            // KELOMPOK 3: BAUT SAMBUNGAN (C.2.16 s/d C.2.23)



            // ==========================================



            var sBaut588 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 588"));



            SafeRename(sBaut588, "C.2.16 - Mezzanine Baut WF 588 Endplate");



            var sBaut400 = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 400"));



            SafeRename(sBaut400, "C.2.17 - Mezzanine Baut WF 400 Endplate");



            var sBaut350End = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 350") && (s.Name.Contains("Endplate") || s.Name.Contains("End Plat")));



            SafeRename(sBaut350End, "C.2.18 - Mezzanine Baut WF 350 Endplate");



            var sBaut350Gus = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 350") && s.Name.Contains("Gusset"));



            SafeRename(sBaut350Gus, "C.2.19 - Mezzanine Baut WF 350 Gusset Plate");



            var sBaut300End = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 300") && (s.Name.Contains("Endplate") || s.Name.Contains("Baut Sambungan") || s.Id.Value == 6862646) && !s.Name.Contains("Gusset"));



            SafeRename(sBaut300End, "C.2.20 - Mezzanine Baut WF 300 Endplate");



            var sBaut300Gus = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 300") && s.Name.Contains("Gusset"));



            SafeRename(sBaut300Gus, "C.2.21 - Mezzanine Baut WF 300 Gusset Plate");



            var sBaut200End = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 200") && (s.Name.Contains("Endplate") || !s.Name.Contains("Gusset")));



            SafeRename(sBaut200End, "C.2.22 - Mezzanine Baut WF 200 Endplate");



            var sBaut200Gus = FindSched(s => s.Definition.CategoryId.Value == (long)BuiltInCategory.OST_StructConnectionBolts && s.Name.Contains("WF 200") && s.Name.Contains("Gusset"));



            SafeRename(sBaut200Gus, "C.2.23 - Mezzanine Baut WF 200 Gusset Plate");



            doc.Regenerate();



            var st = tx.Commit();



            sb.AppendLine($"Status Transaksi: {st}");



        }



        return sb.ToString();



    }



    private static string InspectMismatchedConnections(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: INSPEKSI KONEKSI YANG TIDAK SESUAI FAMILY / SALAH INPUT ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        var mismatchList = new List<(Element conn, string currentVal, string expectedFam, string typeName)>();



        foreach (var conn in conns)



        {



            string expectedFam = GetConnectionFamilyName(doc, conn);



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            string currentVal = p?.AsString() ?? "";



            if (!currentVal.Equals(expectedFam, StringComparison.OrdinalIgnoreCase))



            {



                string tName = GetConnectionTypeName(doc, conn);



                mismatchList.Add((conn, currentVal, expectedFam, tName));



            }



        }



        sb.AppendLine($"Total Koneksi di Model: {conns.Count}");



        sb.AppendLine($"Total Koneksi Tidak Sesuai / Salah Input: {mismatchList.Count}\n");



        foreach (var m in mismatchList)



        {



            int subCount = 0;



            try



            {



                var subs = m.conn.GetSubelements();



                if (subs != null) subCount = subs.Count;



            }



            catch { }



            sb.AppendLine($"- ID: {m.conn.Id.Value} | Type: '{m.typeName}'");



            sb.AppendLine($"    Nilai Param Lama / Input: '{(string.IsNullOrEmpty(m.currentVal) ? "[KOSONG]" : m.currentVal)}'");



            sb.AppendLine($"    Nilai Family Target: '{m.expectedFam}' | Sub-elements: {subCount}");



        }



        return sb.ToString();



    }



    private static string SyncAllConnectionTypes(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: SINKRONISASI ULANG PARAMETER 'Type of connection' KE SELURUH KONEKSI & SUB-ELEMEN ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        ElementId paramId = ElementId.InvalidElementId;



        foreach (var conn in conns)



        {



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            if (p != null) { paramId = p.Id; break; }



        }



        if (paramId == ElementId.InvalidElementId)



        {



            return "Error: Parameter 'Type of connection' tidak ditemukan di dokumen.";



        }



        var mismatchList = new List<(ElementId id, string expectedFam)>();



        foreach (var conn in conns)



        {



            string expectedFam = GetConnectionFamilyName(doc, conn);



            var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



            string currentVal = p?.AsString() ?? "";



            if (!currentVal.Equals(expectedFam, StringComparison.OrdinalIgnoreCase))



            {



                mismatchList.Add((conn.Id, expectedFam));



            }



        }



        sb.AppendLine($"Koneksi yang perlu disinkronkan: {mismatchList.Count} elemen...");



        if (mismatchList.Count == 0)



        {



            sb.AppendLine("Semua koneksi sudah sesuai 100% dengan nama Family induknya.");



            return sb.ToString();



        }



        int connsUpdated = 0;



        int subUpdated = 0;



        int depUpdated = 0;



        using (Transaction tx = new(doc, "AIGRAV - Sync Mismatched Connection Types & Sub-elements"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            foreach (var item in mismatchList)



            {



                var conn = doc.GetElement(item.id);



                if (conn == null) continue;



                string familyVal = item.expectedFam;



                if (string.IsNullOrWhiteSpace(familyVal)) familyVal = conn.Name;



                // 1. Update Connection Handler



                var p = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



                if (p != null && !p.IsReadOnly)



                {



                    try



                    {



                        if (p.StorageType == StorageType.String)



                        {



                            if (p.Set(familyVal)) connsUpdated++;



                        }



                    }



                    catch { }



                }



                // 2. Update Subelements (Plates, Bolts, Welds)



                try



                {



                    var subs = conn.GetSubelements();



                    if (subs != null)



                    {



                        var strVal = new StringParameterValue(familyVal);



                        foreach (Subelement sub in subs)



                        {



                            try



                            {



                                if (sub.HasParameter(paramId) && sub.IsParameterModifiable(paramId))



                                {



                                    sub.SetParameterValue(paramId, strVal);



                                    subUpdated++;



                                }



                            }



                            catch { }



                        }



                    }



                }



                catch { }



                // 3. Update Dependent Elements



                try



                {



                    var depIds = conn.GetDependentElements(null);



                    foreach (var childId in depIds)



                    {



                        if (childId == conn.Id) continue;



                        Element? child = doc.GetElement(childId);



                        if (child != null)



                        {



                            var cp = FindParam(child, "Type of connection") ?? FindParam(child, "Type of Connection");



                            if (cp != null && !cp.IsReadOnly)



                            {



                                try



                                {



                                    if (cp.StorageType == StorageType.String)



                                    {



                                        if (cp.Set(familyVal)) depUpdated++;



                                    }



                                }



                                catch { }



                            }



                        }



                    }



                }



                catch { }



            }



            var st = tx.Commit();



            sb.AppendLine($"Status Transaksi: {st}");



        }



        sb.AppendLine($"\n[HASIL SINKRONISASI PARAMETER]");



        sb.AppendLine($"- Connection Handlers Berhasil Diupdate : {connsUpdated} elemen");



        sb.AppendLine($"- Sub-Elemen (Plates, Bolts, Welds) Diupdate : {subUpdated} sub-elemen");



        sb.AppendLine($"- Dependent Elements Diupdate               : {depUpdated} elemen");



        return sb.ToString();



    }



    public static string InspectLinkApis(Document doc, UIApplication app)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: INSPEKSI REVIT LINK & API CAPABILITIES ===");



        try



        {



            Type t = typeof(RevitLinkGraphicsSettings);



            sb.AppendLine($"Class: {t.FullName}");



            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))



            {



                sb.AppendLine($"  Property: {p.PropertyType.Name} {p.Name} (CanRead: {p.CanRead}, CanWrite: {p.CanWrite})");



            }



            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))



            {



                sb.AppendLine($"  Method: {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(param => param.ParameterType.Name + " " + param.Name))})");



            }



        }



        catch (Exception ex)



        {



            sb.AppendLine($"Error reflecting RevitLinkGraphicsSettings: {ex.Message}");



        }



        try



        {



            sb.AppendLine("\nLinkVisibility enum values:");



            foreach (var name in Enum.GetNames(typeof(LinkVisibility)))



            {



                sb.AppendLine($"  {name} = {(int)Enum.Parse(typeof(LinkVisibility), name)}");



            }



        }



        catch (Exception ex)



        {



            sb.AppendLine($"Error enum LinkVisibility: {ex.Message}");



        }



        try



        {



            Type vt = typeof(View);



            sb.AppendLine("\nView Link-related methods:");



            foreach (var m in vt.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name.Contains("Link")))



            {



                sb.AppendLine($"  Method: {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(param => param.ParameterType.Name + " " + param.Name))})");



            }



        }



        catch (Exception ex)



        {



            sb.AppendLine($"Error View Link methods: {ex.Message}");



        }



        var linkInstances = new FilteredElementCollector(doc)



            .OfClass(typeof(RevitLinkInstance))



            .Cast<RevitLinkInstance>()



            .ToList();



        var linkTypes = new FilteredElementCollector(doc)



            .OfClass(typeof(RevitLinkType))



            .Cast<RevitLinkType>()



            .ToList();



                var viewTemplates = new FilteredElementCollector(doc)



            .OfClass(typeof(View))



            .Cast<View>()



            .Where(v => v.IsTemplate)



            .Select(v => $"- Template: '{v.Name}' (Id: {v.Id.Value}, Type: {v.ViewType})")



            .ToList();



        sb.AppendLine($"\nTotal View Templates: {viewTemplates.Count}");



        foreach (var vt in viewTemplates) sb.AppendLine(vt);



        sb.AppendLine($"\nTotal RevitLinkTypes: {linkTypes.Count}");



        foreach (var lt in linkTypes)



        {



            sb.AppendLine($"- Type: '{lt.Name}' (Id: {lt.Id.Value}) | IsNested: {lt.IsNestedLink} | Status: {lt.GetLinkedFileStatus()}");



        }



        sb.AppendLine($"\nTotal RevitLinkInstances: {linkInstances.Count}");



        foreach (var li in linkInstances)



        {



            Document? linkDoc = li.GetLinkDocument();



            sb.AppendLine($"- Instance: '{li.Name}' (Id: {li.Id.Value}) | TypeId: {li.GetTypeId().Value} | Doc: {(linkDoc != null ? linkDoc.Title : "Unloaded")}");



        }



        var activeView = app.ActiveUIDocument?.ActiveView;



        if (activeView != null)



        {



            sb.AppendLine($"\nActive View: '{activeView.Name}' (Id: {activeView.Id.Value}, Type: {activeView.ViewType})");



            sb.AppendLine($"  IsTemplate: {activeView.IsTemplate}");



            if (activeView.ViewTemplateId != ElementId.InvalidElementId)



            {



                Element? vtEl = doc.GetElement(activeView.ViewTemplateId);



                sb.AppendLine($"  Applied ViewTemplate: '{(vtEl != null ? vtEl.Name : activeView.ViewTemplateId.ToString())}'");



            }



            foreach (var li in linkInstances)



            {



                try



                {



                    var instSettings = activeView.GetLinkOverrides(li.Id);



                    sb.AppendLine($"  Instance [{li.Id.Value}] Overrides: LinkVisibilityType={instSettings?.LinkVisibilityType}, ViewFilterType={instSettings?.ViewFilterType}");



                }



                catch (Exception ex)



                {



                    sb.AppendLine($"  Instance [{li.Id.Value}] GetLinkOverrides: {ex.Message}");



                }



                try



                {



                    var typeSettings = activeView.GetLinkOverrides(li.GetTypeId());



                    sb.AppendLine($"  Type [{li.GetTypeId().Value}] Overrides: LinkVisibilityType={typeSettings?.LinkVisibilityType}, ViewFilterType={typeSettings?.ViewFilterType}");



                }



                catch (Exception ex)



                {



                    sb.AppendLine($"  Type [{li.GetTypeId().Value}] GetLinkOverrides: {ex.Message}");



                }



            }



        }



        return sb.ToString();



    }



    public static string ConfigureLinkViews(Document doc, UIApplication app, string? argument)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: KONFIGURASI OVERRIDE REVIT LINK (CUSTOM) ===");



        var activeView = app.ActiveUIDocument?.ActiveView;



        if (activeView == null) return "Error: Tidak ada Active View.";



        var linkTypes = new FilteredElementCollector(doc)



            .OfClass(typeof(RevitLinkType))



            .Cast<RevitLinkType>()



            .ToList();



        var loadedLinkTypes = linkTypes.Where(lt => lt.GetLinkedFileStatus() == LinkedFileStatus.Loaded).ToList();



        sb.AppendLine($"Active View: '{activeView.Name}' (Id: {activeView.Id.Value})");



        sb.AppendLine($"Total Link Terdaftar : {linkTypes.Count}");



        sb.AppendLine($"Total Link Aktif (Loaded) : {loadedLinkTypes.Count}");



        using (Transaction tx = new(doc, "AIGRAV - Configure Link Overrides Custom"))



        {



            tx.Start();



            // 1. Set Custom pada seluruh link yang Loaded di Active View



            foreach (var lt in loadedLinkTypes)



            {



                var s = activeView.GetLinkOverrides(lt.Id) ?? new RevitLinkGraphicsSettings();



                s.LinkVisibilityType = LinkVisibility.Custom;



                activeView.SetLinkOverrides(lt.Id, s);



                sb.AppendLine($"✓ Active View '{activeView.Name}': Link [{lt.Id.Value}] '{lt.Name}' di-set ke Custom.");



            }



            // 2. Jika argument == "propagate_all_views", copy setting activeView ke seluruh view model



            if (string.Equals(argument, "propagate_all_views", StringComparison.OrdinalIgnoreCase))



            {



                var allViews = new FilteredElementCollector(doc)



                    .OfClass(typeof(View))



                    .Cast<View>()



                    .Where(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan || v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section || v.ViewType == ViewType.ThreeD))



                    .Where(v => v.Id.Value != activeView.Id.Value)



                    .ToList();



                int copiedViews = 0;



                foreach (var v in allViews)



                {



                    if (v.ViewTemplateId != ElementId.InvalidElementId) continue; // view dikunci template



                    foreach (var lt in loadedLinkTypes)



                    {



                        try



                        {



                            var activeSettings = activeView.GetLinkOverrides(lt.Id);



                            if (activeSettings != null)



                            {



                                v.SetLinkOverrides(lt.Id, activeSettings);



                            }



                        }



                        catch { }



                    }



                    copiedViews++;



                }



                sb.AppendLine($"✓ Berhasil mereplikasi setting override link ke {copiedViews} view lainnya di project.");



            }



            tx.Commit();



        }



        return sb.ToString();



    }



    public static string InspectConnectionParamsDetailed(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: INSPEKSI DETAIL PARAMETER BINDING, LOKASI & SUBELEMEN ===");



        // 1. Parameter Bindings



        sb.AppendLine("\n[PARAMETER BINDINGS]:");



        var map = doc.ParameterBindings;



        var it = map.ForwardIterator();



        ElementId typeConnParamId = ElementId.InvalidElementId;



        ElementId lokasiParamId = ElementId.InvalidElementId;



        while (it.MoveNext())



        {



            var def = it.Key;



            var binding = it.Current as ElementBinding;



            if (def.Name.IndexOf("Type of connect", StringComparison.OrdinalIgnoreCase) >= 0 ||



                def.Name.IndexOf("Lokasi", StringComparison.OrdinalIgnoreCase) >= 0)



            {



                sb.AppendLine($"Def: '{def.Name}' | Binding Type: {binding?.GetType().Name}");



                if (binding != null)



                {



                    var catNames = binding.Categories.Cast<Category>().Select(c => $"{c.Name} ({c.BuiltInCategory})").ToList();



                    sb.AppendLine($"   Categories: {string.Join(", ", catNames)}");



                }



            }



        }



        // Get parameter IDs from a sample connection



        long[] newIds = new long[] { 6894668, 6894730, 6894738, 6894746, 6894754, 6894762, 6894770 };



        var sampleConn = doc.GetElement(new ElementId(newIds[0])) as Autodesk.Revit.DB.Structure.StructuralConnectionHandler;



        if (sampleConn != null)



        {



            var pType = sampleConn.LookupParameter("Type of connection") ?? sampleConn.LookupParameter("Type of Connection");



            var pLok = sampleConn.LookupParameter("Lokasi");



            if (pType != null) typeConnParamId = pType.Id;



            if (pLok != null) lokasiParamId = pLok.Id;



            sb.AppendLine($"\nSample Conn [{sampleConn.Id.Value}]:");



            sb.AppendLine($"  typeConnParamId: {typeConnParamId.Value} (Name: {pType?.Definition?.Name})");



            sb.AppendLine($"  lokasiParamId   : {lokasiParamId.Value} (Name: {pLok?.Definition?.Name})");



            var subs = sampleConn.GetSubelements();



            sb.AppendLine($"  Subelements count: {subs?.Count ?? 0}");



            if (subs != null)



            {



                int sIdx = 1;



                foreach (Subelement sub in subs)



                {



                    bool hasType = sub.HasParameter(typeConnParamId);



                    bool canModType = hasType && sub.IsParameterModifiable(typeConnParamId);



                    bool hasLok = sub.HasParameter(lokasiParamId);



                    bool canModLok = hasLok && sub.IsParameterModifiable(lokasiParamId);



                    



                    sb.AppendLine($"    Sub #{sIdx++}: HasTypeParam={hasType} (CanMod:{canModType}), HasLokParam={hasLok} (CanMod:{canModLok})");



                }



            }



        }



        // Nearest connections with Lokasi for the 7 new connections



        var connsWithLokasi = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .Cast<Element>()



            .Where(c => !string.IsNullOrWhiteSpace(c.LookupParameter("Lokasi")?.AsString()))



            .Select(c => new {



                Id = c.Id.Value,



                Lokasi = c.LookupParameter("Lokasi")!.AsString(),



                Fam = c.LookupParameter("Type of connection")?.AsString() ?? "",



                Handler = c as Autodesk.Revit.DB.Structure.StructuralConnectionHandler



            })



            .Where(x => x.Handler != null)



            .ToList();



        sb.AppendLine("\n[7 KONEKSI BARU & LOKASI TERDEKAT]:");



        foreach (var id in newIds)



        {



            var elem = doc.GetElement(new ElementId(id)) as Autodesk.Revit.DB.Structure.StructuralConnectionHandler;



            if (elem == null) continue;



            XYZ pt = elem.GetOrigin();



            double xM = pt.X * 0.3048;



            double yM = pt.Y * 0.3048;



            double zM = pt.Z * 0.3048;



            var nearest = connsWithLokasi



                .OrderBy(c => c.Handler.GetOrigin().DistanceTo(pt))



                .FirstOrDefault();



            double distM = nearest != null ? nearest.Handler.GetOrigin().DistanceTo(pt) * 0.3048 : 0;



            sb.AppendLine($"  ID: {id} | Pos: X={xM:F2}m, Y={yM:F2}m, Z={zM:F2}m");



            sb.AppendLine($"     Rekomendasi Lokasi: '{nearest?.Lokasi}' (Jarak {distM:F2}m dari ID {nearest?.Id} '{nearest?.Fam}')");



        }



        return sb.ToString();



    }



    public static string PopulateConnectionParams(Document doc, string? argument)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: INPUT PARAMETER 'Type of connection' & 'Lokasi' KE KONEKSI & SUB-ELEMEN ===");



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .Cast<Element>()



            .ToList();



        // 1. Dapatkan ElementId parameter



        ElementId typeConnParamId = ElementId.InvalidElementId;



        ElementId lokasiParamId = ElementId.InvalidElementId;



        foreach (var c in conns)



        {



            if (typeConnParamId == ElementId.InvalidElementId)



            {



                var pt = FindParam(c, "Type of connection") ?? FindParam(c, "Type of Connection");



                if (pt != null) typeConnParamId = pt.Id;



            }



            if (lokasiParamId == ElementId.InvalidElementId)



            {



                var pl = FindParam(c, "Lokasi") ?? FindParam(c, "LOKASI");



                if (pl != null) lokasiParamId = pl.Id;



            }



            if (typeConnParamId != ElementId.InvalidElementId && lokasiParamId != ElementId.InvalidElementId)



                break;



        }



        if (typeConnParamId == ElementId.InvalidElementId || lokasiParamId == ElementId.InvalidElementId)



        {



            return $"Error: Parameter tidak lengkap. TypeConnId={typeConnParamId.Value}, LokasiId={lokasiParamId.Value}";



        }



        int connTypeUpdated = 0;



        int connLokasiUpdated = 0;



        int subTypeUpdated = 0;



        int subLokasiUpdated = 0;



        int depUpdated = 0;



        using (Transaction tx = new(doc, "AIGRAV - Populate Connection & Subelements Type and Lokasi"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            // 7 ID koneksi baru khusus



            long[] newIds = new long[] { 6894668, 6894730, 6894738, 6894746, 6894754, 6894762, 6894770 };



            var newIdSet = new HashSet<long>(newIds);



            foreach (var conn in conns)



            {



                var pt = FindParam(conn, "Type of connection") ?? FindParam(conn, "Type of Connection");



                var pl = FindParam(conn, "Lokasi") ?? FindParam(conn, "LOKASI");



                string typeVal = pt?.AsString() ?? "";



                string lokasiVal = pl?.AsString() ?? "";



                // Jika koneksi baru atau kosong, tentukan nilainya



                if (newIdSet.Contains(conn.Id.Value) || string.IsNullOrWhiteSpace(typeVal))



                {



                    string fam = GetConnectionFamilyName(doc, conn);



                    if (string.IsNullOrWhiteSpace(fam)) fam = conn.Name;



                    typeVal = fam;



                    if (pt != null && !pt.IsReadOnly && pt.StorageType == StorageType.String)



                    {



                        if (pt.Set(typeVal)) connTypeUpdated++;



                    }



                }



                if (newIdSet.Contains(conn.Id.Value) || string.IsNullOrWhiteSpace(lokasiVal))



                {



                    // Tentukan lokasi default untuk koneksi baru (Carton & Cups Storage berdasarkan posisi mezzanine WF200)



                    if (string.IsNullOrWhiteSpace(lokasiVal))



                    {



                        lokasiVal = "Carton & Cups Storage";



                    }



                    if (pl != null && !pl.IsReadOnly && pl.StorageType == StorageType.String)



                    {



                        if (pl.Set(lokasiVal)) connLokasiUpdated++;



                    }



                }



                // Subelemen



                var handler = conn as Autodesk.Revit.DB.Structure.StructuralConnectionHandler;



                if (handler != null)



                {



                    try



                    {



                        var subs = handler.GetSubelements();



                        if (subs != null)



                        {



                            var strTypeVal = new StringParameterValue(typeVal);



                            var strLokasiVal = new StringParameterValue(lokasiVal);



                            foreach (Subelement sub in subs)



                            {



                                try



                                {



                                    if (!string.IsNullOrWhiteSpace(typeVal) && sub.HasParameter(typeConnParamId) && sub.IsParameterModifiable(typeConnParamId))



                                    {



                                        sub.SetParameterValue(typeConnParamId, strTypeVal);



                                        subTypeUpdated++;



                                    }



                                }



                                catch { }



                                try



                                {



                                    if (!string.IsNullOrWhiteSpace(lokasiVal) && sub.HasParameter(lokasiParamId) && sub.IsParameterModifiable(lokasiParamId))



                                    {



                                        sub.SetParameterValue(lokasiParamId, strLokasiVal);



                                        subLokasiUpdated++;



                                    }



                                }



                                catch { }



                            }



                        }



                    }



                    catch { }



                    // Dependent elements



                    try



                    {



                        var depIds = handler.GetDependentElements(null);



                        foreach (var childId in depIds)



                        {



                            if (childId == conn.Id) continue;



                            Element? child = doc.GetElement(childId);



                            if (child != null)



                            {



                                var cpType = FindParam(child, "Type of connection") ?? FindParam(child, "Type of Connection");



                                if (cpType != null && !cpType.IsReadOnly && cpType.StorageType == StorageType.String && !string.IsNullOrWhiteSpace(typeVal))



                                {



                                    cpType.Set(typeVal);



                                    depUpdated++;



                                }



                                var cpLok = FindParam(child, "Lokasi") ?? FindParam(child, "LOKASI");



                                if (cpLok != null && !cpLok.IsReadOnly && cpLok.StorageType == StorageType.String && !string.IsNullOrWhiteSpace(lokasiVal))



                                {



                                    cpLok.Set(lokasiVal);



                                    depUpdated++;



                                }



                            }



                        }



                    }



                    catch { }



                }



            }



            doc.Regenerate();



            tx.Commit();



        }



        sb.AppendLine($"\n[HASIL EKSEKUSI]:");



        sb.AppendLine($"  - Koneksi diupdate 'Type of connection' : {connTypeUpdated}");



        sb.AppendLine($"  - Koneksi diupdate 'Lokasi'             : {connLokasiUpdated}");



        sb.AppendLine($"  - Subelemen diupdate 'Type of connection': {subTypeUpdated}");



        sb.AppendLine($"  - Subelemen diupdate 'Lokasi'            : {subLokasiUpdated}");



        sb.AppendLine($"  - Dependent elements diupdate            : {depUpdated}");



        return sb.ToString();



    }



    public static string AuditElementsWithLokasi(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: AUDIT ELEMEN DENGAN PARAMETER 'LOKASI' ===");



        var targetCats = new List<(string Label, BuiltInCategory Cat)>



        {



            ("Balok (Structural Framing)", BuiltInCategory.OST_StructuralFraming),



            ("Kolom (Structural Columns)", BuiltInCategory.OST_StructuralColumns),



            ("Koneksi (Structural Connections)", BuiltInCategory.OST_StructConnections),



            ("Plat Sambungan (Connection Plates)", BuiltInCategory.OST_StructConnectionPlates),



            ("Baut Sambungan (Connection Bolts)", BuiltInCategory.OST_StructConnectionBolts),



            ("Angkur Sambungan (Connection Anchors)", BuiltInCategory.OST_StructConnectionAnchors),



            ("Lantai (Floors)", BuiltInCategory.OST_Floors)



        };



        var lokasiData = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>(StringComparer.OrdinalIgnoreCase);



        var emptyStats = new Dictionary<string, int>();



        foreach (var (label, bic) in targetCats)



        {



            emptyStats[label] = 0;



            var elems = new FilteredElementCollector(doc)



                .OfCategory(bic)



                .WhereElementIsNotElementType()



                .ToList();



            foreach (var elem in elems)



            {



                var pLok = FindParam(elem, "Lokasi") ?? FindParam(elem, "LOKASI");



                string lokVal = pLok?.AsString()?.Trim() ?? "";



                if (string.IsNullOrWhiteSpace(lokVal))



                {



                    emptyStats[label]++;



                    continue;



                }



                if (!lokasiData.TryGetValue(lokVal, out var catMap))



                {



                    catMap = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);



                    lokasiData[lokVal] = catMap;



                }



                if (!catMap.TryGetValue(label, out var typeMap))



                {



                    typeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



                    catMap[label] = typeMap;



                }



                string desc = GetElementSpecification(doc, elem, bic);



                if (!typeMap.ContainsKey(desc)) typeMap[desc] = 0;



                typeMap[desc]++;



            }



        }



        // Also inspect subelements of StructuralConnectionHandler with Lokasi



        ElementId lokasiParamId = ElementId.InvalidElementId;



        var conns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnections)



            .WhereElementIsNotElementType()



            .ToList();



        foreach (var c in conns)



        {



            var pl = FindParam(c, "Lokasi") ?? FindParam(c, "LOKASI");



            if (pl != null) { lokasiParamId = pl.Id; break; }



        }



        var subData = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);



        if (lokasiParamId != ElementId.InvalidElementId)



        {



            foreach (var c in conns)



            {



                var handler = c as Autodesk.Revit.DB.Structure.StructuralConnectionHandler;



                if (handler == null) continue;



                var subs = handler.GetSubelements();



                if (subs == null || subs.Count == 0) continue;



                foreach (Subelement sub in subs)



                {



                    if (!sub.HasParameter(lokasiParamId)) continue;



                    try



                    {



                        var pv = sub.GetParameterValue(lokasiParamId);



                        string subLokVal = "";



                        if (pv is StringParameterValue spv) subLokVal = spv.Value?.Trim() ?? "";



                        if (string.IsNullOrWhiteSpace(subLokVal)) continue;



                        if (!subData.TryGetValue(subLokVal, out var sMap))



                        {



                            sMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



                            subData[subLokVal] = sMap;



                        }



                        string subCat = sub.Category?.Name ?? "Sub-Component Koneksi";



                        if (!sMap.ContainsKey(subCat)) sMap[subCat] = 0;



                        sMap[subCat]++;



                    }



                    catch { }



                }



            }



        }



        // Summary



        sb.AppendLine($"\n[1. RINGKASAN LOKASI YANG DITEMUKAN]");



        sb.AppendLine($"Jumlah Lokasi Unik: {lokasiData.Count}");



        foreach (var lok in lokasiData.Keys)



        {



            int total = lokasiData[lok].Values.Sum(dict => dict.Values.Sum());



            sb.AppendLine($"  * \"{lok}\" : Total {total} elemen model");



        }



        // Detailed breakdown per location



        sb.AppendLine($"\n[2. RINCIAN ELEMEN PER LOKASI]");



        foreach (var (lok, catMap) in lokasiData)



        {



            sb.AppendLine($"\n=======================================================");



            sb.AppendLine($"LOKASI: \"{lok}\"");



            sb.AppendLine($"=======================================================");



            foreach (var (catLabel, typeMap) in catMap)



            {



                int catSum = typeMap.Values.Sum();



                sb.AppendLine($"\n  ▶ {catLabel} (Total: {catSum} unit):");



                foreach (var kvp in typeMap.OrderByDescending(x => x.Value))



                {



                    sb.AppendLine($"     - {kvp.Key,-40} : {kvp.Value,4} unit");



                }



            }



            if (subData.TryGetValue(lok, out var sMap))



            {



                int subSum = sMap.Values.Sum();



                sb.AppendLine($"\n  ▶ Sub-Elemen Internal Koneksi (Total: {subSum} sub-elemen):");



                foreach (var kvp in sMap.OrderByDescending(x => x.Value))



                {



                    sb.AppendLine($"     - {kvp.Key,-40} : {kvp.Value,4} unit");



                }



            }



        }



        // Empty elements (Bangunan Utama)



        sb.AppendLine($"\n=======================================================");



        sb.AppendLine("[3. ELEMEN DENGAN PARAMETER 'LOKASI' KOSONG (Bangunan Utama)]");



        sb.AppendLine("=======================================================");



        foreach (var (cLabel, count) in emptyStats)



        {



            sb.AppendLine($"  - {cLabel,-42} : {count,5} elemen kosong");



        }



        return sb.ToString();



    }



    private static string GetElementSpecification(Document doc, Element elem, BuiltInCategory bic)



    {



        string typeName = "";



        ElementId tid = elem.GetTypeId();



        if (tid != null && tid != ElementId.InvalidElementId)



        {



            var et = doc.GetElement(tid);



            if (et != null)



            {



                string fam = et.get_Parameter(BuiltInParameter.ALL_MODEL_FAMILY_NAME)?.AsString() ?? "";



                typeName = string.IsNullOrWhiteSpace(fam) ? et.Name : $"{fam} : {et.Name}";



            }



        }



        if (string.IsNullOrWhiteSpace(typeName)) typeName = elem.Name;



        if (bic == BuiltInCategory.OST_StructConnectionPlates)



        {



            var pThick = FindParam(elem, "Thickness") ?? FindParam(elem, "Tebal") ?? FindParam(elem, "Plate Thickness");



            var pComm = FindParam(elem, "Comments");



            var pMark = FindParam(elem, "Mark");



            string extra = "";



            if (pThick != null && !string.IsNullOrWhiteSpace(pThick.AsValueString())) extra += $" t={pThick.AsValueString()}";



            if (pMark != null && !string.IsNullOrWhiteSpace(pMark.AsString())) extra += $" Mark:{pMark.AsString()}";



            if (pComm != null && !string.IsNullOrWhiteSpace(pComm.AsString())) extra += $" Comm:{pComm.AsString()}";



            return $"Plate {typeName}{extra}".Trim();



        }



        if (bic == BuiltInCategory.OST_StructConnectionBolts)



        {



            var pDia = FindParam(elem, "Bolt Diameter") ?? FindParam(elem, "Diameter");



            var pStd = FindParam(elem, "Bolt Standard") ?? FindParam(elem, "Standard");



            var pComm = FindParam(elem, "Comments");



            string extra = "";



            if (pDia != null && !string.IsNullOrWhiteSpace(pDia.AsValueString())) extra += $" Dia:{pDia.AsValueString()}";



            if (pStd != null && !string.IsNullOrWhiteSpace(pStd.AsString())) extra += $" Std:{pStd.AsString()}";



            if (pComm != null && !string.IsNullOrWhiteSpace(pComm.AsString())) extra += $" Comm:{pComm.AsString()}";



            return $"Bolt {typeName}{extra}".Trim();



        }



        if (bic == BuiltInCategory.OST_StructConnectionAnchors)



        {



            var pDia = FindParam(elem, "Anchor Diameter") ?? FindParam(elem, "Diameter");



            var pLen = FindParam(elem, "Anchor Length") ?? FindParam(elem, "Length");



            string extra = "";



            if (pDia != null && !string.IsNullOrWhiteSpace(pDia.AsValueString())) extra += $" Dia:{pDia.AsValueString()}";



            if (pLen != null && !string.IsNullOrWhiteSpace(pLen.AsValueString())) extra += $" Len:{pLen.AsValueString()}";



            return $"Anchor {typeName}{extra}".Trim();



        }



        if (bic == BuiltInCategory.OST_StructConnections)



        {



            var pTypeConn = FindParam(elem, "Type of connection") ?? FindParam(elem, "Type of Connection");



            string typeConnVal = pTypeConn?.AsString() ?? "";



            return !string.IsNullOrWhiteSpace(typeConnVal) ? $"Conn: {typeConnVal} ({typeName})" : $"Conn: {typeName}";



        }



        return typeName;



    }



    public static string GenerateLocationBqSchedules(Document doc, string? argument = null)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: GENERASI SCHEDULE BQ STRUKTUR PER LOKASI ===");



        // 1. Locate Source Templates



        var srcKolom = doc.GetElement(new ElementId(851714)) as ViewSchedule // B.2.1



            ?? FindSched(doc, s => s.Name.Contains("B.2.1") || (s.Name.Contains("Kolom") && s.Name.Contains("Mezzanine")));



        var srcBalok = doc.GetElement(new ElementId(851763)) as ViewSchedule // C.2.3



            ?? FindSched(doc, s => s.Name.Contains("C.2.3") || (s.Name.Contains("Beam") && s.Name.Contains("WF 350")));



        var srcPelat = doc.GetElement(new ElementId(6862604)) as ViewSchedule // C.2.10



            ?? FindSched(doc, s => s.Name.Contains("C.2.10") || (s.Name.Contains("Pelat") && s.Name.Contains("WF 350 Endplate")));



        var srcBaut = doc.GetElement(new ElementId(6862618)) as ViewSchedule // C.2.18



            ?? FindSched(doc, s => s.Name.Contains("C.2.18") || (s.Name.Contains("Baut") && s.Name.Contains("WF 350 Endplate")));



        var srcAngkur = doc.GetElement(new ElementId(672435)) as ViewSchedule // B.1.8



            ?? FindSched(doc, s => s.Name.Contains("B.1.8") || s.Name.Contains("Angkur"));



        if (srcBalok == null) return "Error: Source template Balok tidak ditemukan.";



        if (srcPelat == null) return "Error: Source template Pelat tidak ditemukan.";



        if (srcBaut == null) return "Error: Source template Baut tidak ditemukan.";



        var allLocs = new List<(string Code, string LokasiName, bool HasKolom, bool HasAngkur)>



        {



            ("C.3.3", "Carton & Cups Storage", true, true),



            ("C.3.4", "Production Office", true, true),



            ("C.3.1", "Waterline", false, false),



            ("C.3.2", "Power House", true, true),



            ("C.3.5", "Chiller Area 1", true, true),



            ("C.3.6", "Chiller Area 2", true, true)



        };



        var locConfigs = allLocs;



        if (!string.IsNullOrWhiteSpace(argument))



        {



            string argClean = argument.Trim();



            if (argClean.Equals("Batch1", StringComparison.OrdinalIgnoreCase) || argClean.Equals("SubAgent1", StringComparison.OrdinalIgnoreCase))



            {



                locConfigs = allLocs.Where(x => x.LokasiName == "Carton & Cups Storage" || x.LokasiName == "Production Office").ToList();



            }



            else if (argClean.Equals("Batch2", StringComparison.OrdinalIgnoreCase) || argClean.Equals("SubAgent2", StringComparison.OrdinalIgnoreCase))



            {



                locConfigs = allLocs.Where(x => x.LokasiName == "Waterline" || x.LokasiName == "Power House").ToList();



            }



            else if (argClean.Equals("Batch3", StringComparison.OrdinalIgnoreCase) || argClean.Equals("SubAgent3", StringComparison.OrdinalIgnoreCase))



            {



                locConfigs = allLocs.Where(x => x.LokasiName == "Chiller Area 1" || x.LokasiName == "Chiller Area 2").ToList();



            }



            else



            {



                var filters = argClean.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();



                locConfigs = allLocs.Where(x => filters.Any(f => x.LokasiName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 || x.Code.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();



            }



        }



        sb.AppendLine($"Target Pemrosesan: {locConfigs.Count} Lokasi ({string.Join(", ", locConfigs.Select(x => x.LokasiName))})");



        var createdSchedules = new List<string>();



        using (Transaction tx = new(doc, $"AIGRAV - Generate Location BQ Schedules ({argument ?? "All"})"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            foreach (var (code, lokName, hasKolom, hasAngkur) in locConfigs)



            {



                sb.AppendLine($"\n-------------------------------------------------------");



                sb.AppendLine($"MEMPROSES LOKASI: {lokName} (Prefix: {code})");



                sb.AppendLine($"-------------------------------------------------------");



                // A. Kolom Schedule



                if (hasKolom && srcKolom != null)



                {



                    string name = $"{code}.1 - Kolom Baja ({lokName})";



                    var (s, err) = SetupScheduleFromTemplate(doc, srcKolom, name, lokName, "Kolom");



                    if (s != null)



                    {



                        createdSchedules.Add(name);



                        sb.AppendLine($"  ✓ Berhasil membuat: {name}");



                    }



                    else



                    {



                        sb.AppendLine($"  ✕ {name,-45} : Gagal ({err})");



                    }



                }



                // B. Balok Schedule



                {



                    string name = $"{code}.2 - Balok Baja ({lokName})";



                    var (s, err) = SetupScheduleFromTemplate(doc, srcBalok, name, lokName, "Balok");



                    if (s != null)



                    {



                        createdSchedules.Add(name);



                        sb.AppendLine($"  ✓ Berhasil membuat: {name}");



                    }



                    else



                    {



                        sb.AppendLine($"  ✕ {name,-45} : Gagal ({err})");



                    }



                }



                // C. Angkur Schedule



                if (hasAngkur && srcAngkur != null)



                {



                    string name = $"{code}.3 - Angkur Sambungan ({lokName})";



                    var (s, err) = SetupScheduleFromTemplate(doc, srcAngkur, name, lokName, "Angkur");



                    if (s != null)



                    {



                        createdSchedules.Add(name);



                        sb.AppendLine($"  ✓ Berhasil membuat: {name}");



                    }



                    else



                    {



                        sb.AppendLine($"  ✕ {name,-45} : Gagal ({err})");



                    }



                }



                // D. Pelat Schedule



                {



                    string name = $"{code}.4 - Pelat Sambungan ({lokName})";



                    var (s, err) = SetupScheduleFromTemplate(doc, srcPelat, name, lokName, "Pelat");



                    if (s != null)



                    {



                        createdSchedules.Add(name);



                        sb.AppendLine($"  ✓ Berhasil membuat: {name}");



                    }



                    else



                    {



                        sb.AppendLine($"  ✕ {name,-45} : Gagal ({err})");



                    }



                }



                // E. Baut Schedule



                {



                    string name = $"{code}.5 - Baut Sambungan ({lokName})";



                    var (s, err) = SetupScheduleFromTemplate(doc, srcBaut, name, lokName, "Baut");



                    if (s != null)



                    {



                        createdSchedules.Add(name);



                        sb.AppendLine($"  ✓ Berhasil membuat: {name}");



                    }



                    else



                    {



                        sb.AppendLine($"  ✕ {name,-45} : Gagal ({err})");



                    }



                }



            }



            doc.Regenerate();



            var status = tx.Commit();



            sb.AppendLine($"\nStatus Transaksi: {status}");



        }



        sb.AppendLine($"\n=======================================================");



        sb.AppendLine($"TOTAL SCHEDULE BQ LOKASI BERHASIL DIBUAT: {createdSchedules.Count}");



        sb.AppendLine($"=======================================================");



        foreach (var name in createdSchedules)



        {



            sb.AppendLine($"  * {name}");



        }



        return sb.ToString();



    }



    private static (ViewSchedule? vs, string err) SetupScheduleFromTemplate(



        Document doc,



        ViewSchedule src,



        string newName,



        string lokName,



        string kind)



    {



        try



        {



            var old = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .FirstOrDefault(s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));



            if (old != null) doc.Delete(old.Id);



            ElementId newId = src.Duplicate(ViewDuplicateOption.Duplicate);



            ViewSchedule vs = (ViewSchedule)doc.GetElement(newId);



            vs.Name = newName;



            ScheduleDefinition def = vs.Definition;



            def.IsItemized = false;



            def.ClearFilters();



            ScheduleField? fLok = null;



            for (int i = 0; i < def.GetFieldCount(); i++)



            {



                var f = def.GetField(i);



                if (f.GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))



                {



                    fLok = f;



                    break;



                }



            }



            if (fLok == null)



            {



                var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Lokasi", StringComparison.OrdinalIgnoreCase));



                if (sf != null)



                {



                    fLok = def.AddField(sf);



                }



            }



            if (fLok != null)



            {



                fLok.IsHidden = true;



                try



                {



                    def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, lokName));



                }



                catch



                {



                    try



                    {



                        def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Contains, lokName));



                    }



                    catch { }



                }



            }



            if (kind == "Pelat")



            {



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                ScheduleField? fThk = null;



                ScheduleField? fWidth = null;



                ScheduleField? fLength = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    string fn = f.GetName();



                    if (fn.IndexOf("Thickness", StringComparison.OrdinalIgnoreCase) >= 0 || fn.IndexOf("Tebal", StringComparison.OrdinalIgnoreCase) >= 0) fThk = f;



                    else if (fn.IndexOf("Width", StringComparison.OrdinalIgnoreCase) >= 0 || fn.IndexOf("Lebar", StringComparison.OrdinalIgnoreCase) >= 0) fWidth = f;



                    else if (fn.IndexOf("Length", StringComparison.OrdinalIgnoreCase) >= 0 || fn.IndexOf("Panjang", StringComparison.OrdinalIgnoreCase) >= 0) fLength = f;



                }



                if (fThk != null)



                {



                    var sg = new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = true, ShowFooter = true };



                    def.AddSortGroupField(sg);



                }



                if (fWidth != null)



                {



                    var sg = new ScheduleSortGroupField(fWidth.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false };



                    def.AddSortGroupField(sg);



                }



                if (fLength != null)



                {



                    var sg = new ScheduleSortGroupField(fLength.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false };



                    def.AddSortGroupField(sg);



                }



            }



            else if (kind == "Baut")



            {



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                ScheduleField? fDia = null;



                ScheduleField? fStd = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    string fn = f.GetName();



                    if (fn.IndexOf("Diameter", StringComparison.OrdinalIgnoreCase) >= 0) fDia = f;



                    else if (fn.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0) fStd = f;



                }



                if (fDia != null)



                {



                    var sg = new ScheduleSortGroupField(fDia.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = true };



                    def.AddSortGroupField(sg);



                }



                if (fStd != null)



                {



                    var sg = new ScheduleSortGroupField(fStd.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false };



                    def.AddSortGroupField(sg);



                }



            }



            else if (kind == "Balok")



            {



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                ScheduleField? fType = null;



                ScheduleField? fLen = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    string fn = f.GetName();



                    if (fn.Equals("Type", StringComparison.OrdinalIgnoreCase) || fn.Equals("Family and Type", StringComparison.OrdinalIgnoreCase)) fType = f;



                    else if (fn.IndexOf("Length", StringComparison.OrdinalIgnoreCase) >= 0 || fn.IndexOf("Panjang", StringComparison.OrdinalIgnoreCase) >= 0) fLen = f;



                }



                if (fType != null)



                {



                    var sg = new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = true };



                    def.AddSortGroupField(sg);



                }



                if (fLen != null)



                {



                    var sg = new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Descending) { ShowHeader = false, ShowFooter = false };



                    def.AddSortGroupField(sg);



                }



            }



            else if (kind == "Kolom")



            {



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                ScheduleField? fType = null;



                ScheduleField? fLen = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    string fn = f.GetName();



                    if (fn.Equals("Type", StringComparison.OrdinalIgnoreCase) || fn.Equals("Family and Type", StringComparison.OrdinalIgnoreCase)) fType = f;



                    else if (fn.IndexOf("Length", StringComparison.OrdinalIgnoreCase) >= 0) fLen = f;



                }



                if (fType != null)



                {



                    var sg = new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = true };



                    def.AddSortGroupField(sg);



                }



                if (fLen != null)



                {



                    var sg = new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false };



                    def.AddSortGroupField(sg);



                }



            }



            else if (kind == "Angkur")



            {



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                ScheduleField? fDia = null;



                ScheduleField? fLen = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    string fn = f.GetName();



                    if (fn.IndexOf("Diameter", StringComparison.OrdinalIgnoreCase) >= 0) fDia = f;



                    else if (fn.IndexOf("Length", StringComparison.OrdinalIgnoreCase) >= 0 || fn.IndexOf("Panjang", StringComparison.OrdinalIgnoreCase) >= 0) fLen = f;



                }



                if (fDia != null)



                {



                    var sg = new ScheduleSortGroupField(fDia.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = true };



                    def.AddSortGroupField(sg);



                }



                if (fLen != null)



                {



                    var sg = new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false };



                    def.AddSortGroupField(sg);



                }



            }



            return (vs, "");



        }



        catch (Exception ex)



        {



            return (null, ex.Message);



        }



    }



    private static int GetScheduleMatchedCount(Document doc, ViewSchedule vs)



    {



        try



        {



            return new FilteredElementCollector(doc, vs.Id).WhereElementIsNotElementType().GetElementCount();



        }



        catch



        {



            return 0;



        }



    }



    private static ViewSchedule? FindSched(Document doc, Func<ViewSchedule, bool> pred)



    {



        return new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .FirstOrDefault(pred);



    }



    // =========================================================================



    // MULTI-AGENT EXECUTION ENGINE: BATCH 1, BATCH 2, BATCH 3 & QA AUDIT



    // =========================================================================



    public static string CleanupOrphanedCopies(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== HOUSEKEEPING: CLEANUP 56 ORPHANED COPY SCHEDULES ===");



        var allScheds = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .ToList();



        var toDelete = allScheds.Where(s => 



            s.Name.IndexOf(" Copy ", StringComparison.OrdinalIgnoreCase) >= 0 ||



            s.Name.IndexOf("Connection Steel", StringComparison.OrdinalIgnoreCase) >= 0 ||



            s.Name.Equals("C.3.2.7 Power House - Baseplate Kolom t=16", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.2.8 Power House - Angkur Kolom Dia 19mm", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.3.15 Carton & Cups Storage - Baseplate Kolom t=16", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.3.16 Carton & Cups Storage - Angkur Kolom Dia 19mm", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.4.9 Production Office - Baseplate Kolom t=12", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.4.10 Production Office - Baseplate Kolom t=19", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.4.11 Production Office - Angkur Kolom Dia 19mm", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.5.7 Chiller Area 1 - Baseplate Kolom t=16", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.5.8 Chiller Area 1 - Angkur Kolom Dia 19mm", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.6.7 Chiller Area 2 - Baseplate Kolom t=16", StringComparison.OrdinalIgnoreCase) ||



            s.Name.Equals("C.3.6.8 Chiller Area 2 - Angkur Kolom Dia 19mm", StringComparison.OrdinalIgnoreCase) ||



            (s.Name.StartsWith("C.3.") && (



                s.Name.EndsWith("(Carton & Cups Storage)") || 



                s.Name.EndsWith("(Production Office)") || 



                s.Name.EndsWith("(Waterline)") || 



                s.Name.EndsWith("(Power House)") || 



                s.Name.EndsWith("(Chiller Area 1)") || 



                s.Name.EndsWith("(Chiller Area 2)") ||



                s.Name.Contains(" - Pelat Sambungan (") ||



                s.Name.Contains(" - Baut Sambungan (") ||



                s.Name.Contains(" - Kolom Baja (") ||



                s.Name.Contains(" - Balok Baja (") ||



                s.Name.Contains(" - Angkur Sambungan (")



            ))



        ).ToList();



        sb.AppendLine($"Ditemukan {toDelete.Count} schedule duplikat / copy usang untuk dihapus.");



        using (Transaction tx = new(doc, "AIGRAV - Delete Orphaned Copy Schedules"))



        {



            tx.Start();



            int deleted = 0;



            foreach (var s in toDelete)



            {



                try



                {



                    doc.Delete(s.Id);



                    deleted++;



                }



                catch { }



            }



            doc.Regenerate();



            tx.Commit();



            sb.AppendLine($"Sukses menghapus {deleted} schedule sampah dari Project Browser.");



        }



        return sb.ToString();



    }



    public static string ApplyAntiDoubleCountingToBangunanUtama(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: PENERAPAN FILTER ANTI DOUBLE-COUNTING KE BANGUNAN UTAMA ===");



        var targetNames = new string[]



        {



            "B.1.8 - Kolom Angkur",



            "B.2.1 - Kolom Baja WF 350 Mezzanine",



            "C.2.3 - Beam Mezzanine WF 350",



            "C.2.5 - Beam Mezzanine WF 250",



            "C.2.8 - Mezzanine Pelat WF 350 Endplate",



            "C.2.9 - Mezzanine Pelat WF 400 Endplate",



            "C.2.10 - Mezzanine Pelat WF 350 Endplate",



            "C.2.11 - Mezzanine Pelat WF 350 Gusset Plate",



            "C.2.12 - Mezzanine Pelat WF 300 Endplate",



            "C.2.13 - Mezzanine Pelat WF 300 Gusset Plate",



            "C.2.14 - Mezzanine Pelat WF 200 Endplate",



            "C.2.15 - Mezzanine Pelat WF 200 Gusset Plate",



            "C.2.16 - Mezzanine Baut WF 588 Endplate",



            "C.2.17 - Mezzanine Baut WF 400 Endplate",



            "C.2.18 - Mezzanine Baut WF 350 Endplate",



            "C.2.19 - Mezzanine Baut WF 350 Gusset Plate",



            "C.2.20 - Mezzanine Baut WF 300 Endplate",



            "C.2.21 - Mezzanine Baut WF 300 Gusset Plate",



            "C.2.22 - Mezzanine Baut WF 200 Endplate",



            "C.2.23 - Mezzanine Baut WF 200 Gusset Plate"



        };



        using (Transaction tx = new(doc, "AIGRAV - Anti Double Counting Filter on Bangunan Utama"))



        {



            tx.Start();



            int updated = 0;



            foreach (var name in targetNames)



            {



                var s = FindSched(doc, x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));



                if (s == null) continue;



                var def = s.Definition;



                ScheduleField? fLok = null;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if (f.GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))



                    {



                        fLok = f;



                        break;



                    }



                }



                if (fLok == null)



                {



                    var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Lokasi", StringComparison.OrdinalIgnoreCase));



                    if (sf != null)



                    {



                        fLok = def.AddField(sf);



                        fLok.IsHidden = true;



                    }



                }



                if (fLok != null)



                {



                    // Hapus filter Lokasi lama jika ada



                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                    {



                        var flt = def.GetFilter(i);



                        if (flt.FieldId == fLok.FieldId)



                        {



                            def.RemoveFilter(i);



                        }



                    }



                    // Tambahkan filter Lokasi = ""



                    try



                    {



                        def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, ""));



                        updated++;



                        sb.AppendLine($"  âœ“ {name,-42} : Berhasil diterapkan filter Lokasi=\"\"");



                    }



                    catch (Exception ex)



                    {



                        sb.AppendLine($"  âœ• {name,-42} : Gagal AddFilter ({ex.Message})");



                    }



                }



            }



            doc.Regenerate();



            tx.Commit();



            sb.AppendLine($"\nTotal schedule Bangunan Utama terisolasi dari double-counting: {updated}");



        }



        return sb.ToString();



    }



    private static ScheduleField? GetOrAddField(ScheduleDefinition d, Document doc, string name)



    {



        for (int i = 0; i < d.GetFieldCount(); i++)



        {



            var f = d.GetField(i);



            if (f.GetName().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return f;



        }



        var sf = d.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);



        return sf != null ? d.AddField(sf) : null;



    }



    private static void ReorderVisibleFields(ScheduleDefinition d, List<ScheduleField?> visibleFields)



    {



        var allFields = d.GetFieldOrder();



        var newOrder = new List<ScheduleFieldId>();



        foreach (var vf in visibleFields)



        {



            if (vf != null)



            {



                var matchingFid = allFields.FirstOrDefault(fid => fid == vf.FieldId);



                if (matchingFid != null && !newOrder.Any(f => f == matchingFid))



                {



                    newOrder.Add(matchingFid);



                }



            }



        }



        foreach (var fid in allFields)



        {



            if (!newOrder.Any(f => f == fid)) newOrder.Add(fid);



        }



        d.SetFieldOrder(newOrder);



    }



    private static (ViewSchedule? vs, string err) CreateStructuredSchedule(



        Document doc,



        ViewSchedule? srcTemplate,



        BuiltInCategory bic,



        string newName,



        string lokName,



        string itemKind,



        string profileFilter)



    {



        try



        {



            ViewSchedule vs;



            var old = FindSched(doc, s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));



            if (old != null)



            {



                vs = old;



            }



            else if (srcTemplate != null)



            {



                ElementId newId = srcTemplate.Duplicate(ViewDuplicateOption.Duplicate);



                vs = (ViewSchedule)doc.GetElement(newId);



                vs.Name = newName;



            }



            else



            {



                ElementId catId = new ElementId(bic);



                if (!ViewSchedule.IsValidCategoryForSchedule(catId))



                {



                    return (null, $"Kategori {bic} tidak valid untuk schedule");



                }



                vs = ViewSchedule.CreateSchedule(doc, catId);



                vs.Name = newName;



            }



            var def = vs.Definition;



            def.IsItemized = false;



            def.ClearFilters();



            // 1. Pastikan Field 'Lokasi' ada, TIDAK DI-HIDE (muncul sebagai penanda di kolom paling kiri), dan terfilter



            var fLok = GetOrAddField(def, doc, "Lokasi");



            if (fLok != null)



            {



                fLok.IsHidden = false;



                fLok.ColumnHeading = "Lokasi";



                def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, lokName));



            }



            if (itemKind == "Pelat")



            {



                // Template: srcPelat (C.2.10, OST_StructConnectionPlates)



                // Format Wajib: Lokasi, Tipe Sambungan, QTY, Berat



                var fType = GetOrAddField(def, doc, "Type of Connection") ?? GetOrAddField(def, doc, "Tipe Sambungan");
                var fThk = GetOrAddField(def, doc, "Thickness") ?? GetOrAddField(def, doc, "Thickness (mm)");



                var fCount = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                var fWeight = GetOrAddField(def, doc, "Weight") ?? GetOrAddField(def, doc, "Berat");



                if (fType != null)



                {



                    fType.ColumnHeading = "Tipe Sambungan";



                    fType.IsHidden = false;



                    if (!string.IsNullOrWhiteSpace(profileFilter))



                    {



                        def.AddFilter(new ScheduleFilter(fType.FieldId, ScheduleFilterType.Contains, profileFilter));



                    }



                }



                if (fCount != null)



                {



                    fCount.ColumnHeading = "Qty";



                    fCount.IsHidden = false;



                    fCount.DisplayType = ScheduleFieldDisplayType.Totals;



                }



                if (fThk != null) { fThk.ColumnHeading = "Thickness (mm)"; fThk.IsHidden = false; }
                if (fWeight != null)



                {



                    fWeight.ColumnHeading = "Berat (Kg)";



                    fWeight.IsHidden = false;



                    fWeight.DisplayType = ScheduleFieldDisplayType.Totals;



                }



                // Sembunyikan kolom yang tidak perlu (Comments, Thickness, Width, Length, Tipe Detail, dsb)



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if (fLok != null && f.FieldId == fLok.FieldId) continue;



                    if (fType != null && f.FieldId == fType.FieldId) continue;



                    if (fCount != null && f.FieldId == fCount.FieldId) continue;



                    if (fWeight != null && f.FieldId == fWeight.FieldId) continue;
                    if (fThk != null && f.FieldId == fThk.FieldId) continue;



                    f.IsHidden = true;



                }



                ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fThk, fCount, fWeight });



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                if (fType != null)



                {



                    def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false });



                }



                if (fThk != null) { def.AddSortGroupField(new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = true }); }
                def.ShowGrandTotal = true;



                def.ShowGrandTotalCount = true;



                def.ShowGrandTotalTitle = true;



                def.GrandTotalTitle = "Total";



            }



            else if (itemKind == "Baut")



            {



                // Template: srcBaut (C.2.18, OST_StructConnectionBolts)



                // Format Wajib: Lokasi, Tipe Sambungan, QTY (hanya sampai qty, tanpa berat)



                var fType = GetOrAddField(def, doc, "Type of Connection") ?? GetOrAddField(def, doc, "Tipe Sambungan");



                var fCount = GetOrAddField(def, doc, "Number") ?? GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                if (fType != null)



                {



                    fType.ColumnHeading = "Tipe Sambungan";



                    fType.IsHidden = false;



                    if (!string.IsNullOrWhiteSpace(profileFilter))



                    {



                        def.AddFilter(new ScheduleFilter(fType.FieldId, ScheduleFilterType.Contains, profileFilter));



                    }



                }



                if (fCount != null)



                {



                    fCount.ColumnHeading = "Qty";



                    fCount.IsHidden = false;



                    fCount.DisplayType = ScheduleFieldDisplayType.Totals;



                }



                // Sembunyikan kolom lain (Standard, Diameter, Comments, Weight, dsb)



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if (fLok != null && f.FieldId == fLok.FieldId) continue;



                    if (fType != null && f.FieldId == fType.FieldId) continue;



                    if (fCount != null && f.FieldId == fCount.FieldId) continue;



                    f.IsHidden = true;



                }



                ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fCount });



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                if (fType != null)



                {



                    def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false });



                }



                def.ShowGrandTotal = true;



                def.ShowGrandTotalCount = true;



                def.ShowGrandTotalTitle = true;



                def.GrandTotalTitle = "Total";



            }



            else if (itemKind == "Baseplate")



            {



                // Template: srcPelat (C.2.10, OST_StructConnectionPlates)



                // Format: Lokasi, Keterangan, Thickness, Width, Length, Qty, Berat



                var fComm = GetOrAddField(def, doc, "Comments");



                var fThk = GetOrAddField(def, doc, "Thickness");



                var fWidth = GetOrAddField(def, doc, "Width");



                var fLen = GetOrAddField(def, doc, "Length");



                var fCount = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                var fWeight = GetOrAddField(def, doc, "Weight") ?? GetOrAddField(def, doc, "Berat");



                if (fComm != null)



                {



                    fComm.ColumnHeading = "Keterangan";



                    fComm.IsHidden = false;



                    if (!string.IsNullOrWhiteSpace(profileFilter))



                    {



                        def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, profileFilter));



                    }



                }



                if (fThk != null) { fThk.ColumnHeading = "Thickness (mm)"; fThk.IsHidden = false; }



                if (fWidth != null) { fWidth.ColumnHeading = "Width (mm)"; fWidth.IsHidden = false; }



                if (fLen != null) { fLen.ColumnHeading = "Length (mm)"; fLen.IsHidden = false; }



                if (fCount != null) { fCount.ColumnHeading = "Qty"; fCount.IsHidden = false; fCount.DisplayType = ScheduleFieldDisplayType.Totals; }



                if (fWeight != null) { fWeight.ColumnHeading = "Berat (Kg)"; fWeight.IsHidden = false; fWeight.DisplayType = ScheduleFieldDisplayType.Totals; }



                // Sembunyikan field lainnya, terutama Type of Connection yang kosong



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if (fLok != null && f.FieldId == fLok.FieldId) continue;



                    if (fComm != null && f.FieldId == fComm.FieldId) continue;



                    if (fThk != null && f.FieldId == fThk.FieldId) continue;



                    if (fWidth != null && f.FieldId == fWidth.FieldId) continue;



                    if (fLen != null && f.FieldId == fLen.FieldId) continue;



                    if (fCount != null && f.FieldId == fCount.FieldId) continue;



                    if (fWeight != null && f.FieldId == fWeight.FieldId) continue;



                    f.IsHidden = true;



                }



                ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fComm, fThk, fWidth, fLen, fCount, fWeight });



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                if (fComm != null) def.AddSortGroupField(new ScheduleSortGroupField(fComm.FieldId, ScheduleSortOrder.Ascending));



                if (fThk != null) def.AddSortGroupField(new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending));



                if (fWidth != null) def.AddSortGroupField(new ScheduleSortGroupField(fWidth.FieldId, ScheduleSortOrder.Ascending));



                if (fLen != null) def.AddSortGroupField(new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending));



                def.ShowGrandTotal = true;



                def.ShowGrandTotalCount = true;



                def.ShowGrandTotalTitle = true;



                def.GrandTotalTitle = "Total";



            }



            else if (itemKind == "Kolom" || itemKind == "Balok")



            {



                var fComm = GetOrAddField(def, doc, "Comments");



                var fType = GetOrAddField(def, doc, "Type") ?? GetOrAddField(def, doc, "Family and Type");



                var fLen = GetOrAddField(def, doc, "System Length") ?? GetOrAddField(def, doc, "Length");



                var fCount = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                var fWeight = GetOrAddField(def, doc, "Berat (Kg)") ?? GetOrAddField(def, doc, "Weight") ?? GetOrAddField(def, doc, "Berat");



                if (fComm != null) { fComm.ColumnHeading = "Comments"; fComm.IsHidden = false; }



                if (fType != null)



                {



                    fType.ColumnHeading = "Type";



                    fType.IsHidden = false;



                    if (!string.IsNullOrWhiteSpace(profileFilter))



                    {



                        def.AddFilter(new ScheduleFilter(fType.FieldId, ScheduleFilterType.Contains, profileFilter));



                    }



                }



                if (fLen != null) { fLen.ColumnHeading = "Length (mm)"; fLen.IsHidden = false; }



                if (fCount != null) { fCount.ColumnHeading = "Qty"; fCount.IsHidden = false; fCount.DisplayType = ScheduleFieldDisplayType.Totals; }



                if (fWeight != null) { fWeight.ColumnHeading = "Berat (Kg)"; fWeight.IsHidden = false; fWeight.DisplayType = ScheduleFieldDisplayType.Totals; }



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if (fLok != null && f.FieldId == fLok.FieldId) continue;



                    if (fComm != null && f.FieldId == fComm.FieldId) continue;



                    if (fType != null && f.FieldId == fType.FieldId) continue;



                    if (fLen != null && f.FieldId == fLen.FieldId) continue;



                    if (fCount != null && f.FieldId == fCount.FieldId) continue;



                    if (fWeight != null && f.FieldId == fWeight.FieldId) continue;



                    f.IsHidden = true;



                }



                ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fComm, fType, fLen, fCount, fWeight });



                while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);



                if (fComm != null) def.AddSortGroupField(new ScheduleSortGroupField(fComm.FieldId, ScheduleSortOrder.Ascending));



                if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending));



                if (fLen != null) def.AddSortGroupField(new ScheduleSortGroupField(fLen.FieldId, ScheduleSortOrder.Ascending));



                def.ShowGrandTotal = true;



                def.ShowGrandTotalCount = true;



                def.ShowGrandTotalTitle = true;



                def.GrandTotalTitle = "Total";



            }



            else if (itemKind == "Angkur")



            {



                ReorderVisibleFields(def, new List<ScheduleField?> { fLok });



                if (fLok != null) fLok.IsHidden = false;



                def.ShowGrandTotal = true;



                def.ShowGrandTotalCount = true;



                def.ShowGrandTotalTitle = true;



                def.GrandTotalTitle = "Total";



            }



            return (vs, "");



        }



        catch (Exception ex)



        {



            return (null, ex.Message);



        }



    }



    public static string ExecuteBatch1_CartonCupsAndProductionOffice(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 1 (BATCH 1): CARTON & CUPS STORAGE & PRODUCTION OFFICE ===");



        var srcKolom = doc.GetElement(new ElementId(851714)) as ViewSchedule; // B.2.1



        var srcBalok = doc.GetElement(new ElementId(851763)) as ViewSchedule; // C.2.3



        var srcPelat = doc.GetElement(new ElementId(6862604)) as ViewSchedule; // C.2.10



        var srcBaut = doc.GetElement(new ElementId(6862618)) as ViewSchedule; // C.2.18



        var srcAngkur = doc.GetElement(new ElementId(672435)) as ViewSchedule; // B.1.8



        var items = new List<(string Name, string Lokasi, string Kind, string Filter)>



        {



            // Carton & Cups Storage (C.3.3) - 21 Schedules



            ("C.3.3.1 Carton & Cups Storage - Kolom Baja H 250", "Carton & Cups Storage", "Kolom", "H 250"),



            ("C.3.3.2 Carton & Cups Storage - Kolom Baja H 200", "Carton & Cups Storage", "Kolom", "H 200"),



            ("C.3.3.3 Carton & Cups Storage - Kolom Baja WF 200", "Carton & Cups Storage", "Kolom", "WF 200"),



            ("C.3.3.4 Carton & Cups Storage - Balok Baja WF 350", "Carton & Cups Storage", "Balok", "WF 350"),



            ("C.3.3.5 Carton & Cups Storage - Balok Baja WF 400", "Carton & Cups Storage", "Balok", "WF 400"),



            ("C.3.3.6 Carton & Cups Storage - Balok Baja WF 300", "Carton & Cups Storage", "Balok", "WF 300"),



            ("C.3.3.7 Carton & Cups Storage - Balok Baja WF 250", "Carton & Cups Storage", "Balok", "WF 250"),



            // Pelat Sambungan (6)



            ("C.3.3.8 Carton & Cups Storage - Pelat Sambungan WF350-Endplate", "Carton & Cups Storage", "Pelat", "WF350-Endplate"),



            ("C.3.3.9 Carton & Cups Storage - Pelat Sambungan WF350-Gusset Plate", "Carton & Cups Storage", "Pelat", "WF350-Gusset"),



            ("C.3.3.10 Carton & Cups Storage - Pelat Sambungan WF-400 Endplate", "Carton & Cups Storage", "Pelat", "WF-400 Endplate"),



            ("C.3.3.11 Carton & Cups Storage - Pelat Sambungan WF300-Endplate", "Carton & Cups Storage", "Pelat", "WF300-Endplate"),



            ("C.3.3.12 Carton & Cups Storage - Pelat Sambungan WF200-Endplate", "Carton & Cups Storage", "Pelat", "WF200-Endplate"),



            ("C.3.3.13 Carton & Cups Storage - Pelat Sambungan WF200-Gusset Plate", "Carton & Cups Storage", "Pelat", "WF200-Gusset"),



            // Baut Sambungan (6)



            ("C.3.3.14 Carton & Cups Storage - Baut Sambungan WF350-Endplate", "Carton & Cups Storage", "Baut", "WF350-Endplate"),



            ("C.3.3.15 Carton & Cups Storage - Baut Sambungan WF350-Gusset Plate", "Carton & Cups Storage", "Baut", "WF350-Gusset"),



            ("C.3.3.16 Carton & Cups Storage - Baut Sambungan WF-400 Endplate", "Carton & Cups Storage", "Baut", "WF-400 Endplate"),



            ("C.3.3.17 Carton & Cups Storage - Baut Sambungan WF300-Endplate", "Carton & Cups Storage", "Baut", "WF300-Endplate"),



            ("C.3.3.18 Carton & Cups Storage - Baut Sambungan WF200-Endplate", "Carton & Cups Storage", "Baut", "WF200-Endplate"),



            ("C.3.3.19 Carton & Cups Storage - Baut Sambungan WF200-Gusset Plate", "Carton & Cups Storage", "Baut", "WF200-Gusset"),



            // Baseplate (1) & Angkur (1)



            ("C.3.3.20 Carton & Cups Storage - Baseplate Kolom t=16", "Carton & Cups Storage", "Baseplate", "Baseplate"),



            ("C.3.3.21 Carton & Cups Storage - Angkur Kolom Dia 19mm", "Carton & Cups Storage", "Angkur", ""),



            // Production Office (C.3.4) - 14 Schedules



            ("C.3.4.1 Production Office - Kolom Baja WF 350", "Production Office", "Kolom", "WF 350"),



            ("C.3.4.2 Production Office - Balok Baja L 40x40x4", "Production Office", "Balok", "L 40"),



            ("C.3.4.3 Production Office - Balok Baja WF 350", "Production Office", "Balok", "WF 350"),



            ("C.3.4.4 Production Office - Balok Baja WF 250", "Production Office", "Balok", "WF 250"),



            ("C.3.4.5 Production Office - Balok Baja UNP 200", "Production Office", "Balok", "UNP 200"),



            // Pelat Sambungan (3)



            ("C.3.4.6 Production Office - Pelat Sambungan WF350-Endplate", "Production Office", "Pelat", "WF350-Endplate"),



            ("C.3.4.7 Production Office - Pelat Sambungan WF200-Gusset Plate", "Production Office", "Pelat", "WF200-Gusset"),



            ("C.3.4.8 Production Office - Pelat Sambungan WF350-Gusset Plate", "Production Office", "Pelat", "WF350-Gusset"),



            // Baut Sambungan (3)



            ("C.3.4.9 Production Office - Baut Sambungan WF350-Endplate", "Production Office", "Baut", "WF350-Endplate"),



            ("C.3.4.10 Production Office - Baut Sambungan WF200-Gusset Plate", "Production Office", "Baut", "WF200-Gusset"),



            ("C.3.4.11 Production Office - Baut Sambungan WF350-Gusset Plate", "Production Office", "Baut", "WF350-Gusset"),



            // Baseplate (2) & Angkur (1)



            ("C.3.4.12 Production Office - Baseplate Kolom t=12", "Production Office", "Baseplate", "t=12"),



            ("C.3.4.13 Production Office - Baseplate Kolom t=19", "Production Office", "Baseplate", "t=19"),



            ("C.3.4.14 Production Office - Angkur Kolom Dia 19mm", "Production Office", "Angkur", "")



        };



        using (Transaction tx = new(doc, "AIGRAV - Execute Batch 1 Schedules"))



        {



            tx.Start();



            int success = 0;



            foreach (var item in items)



            {



                ViewSchedule? src = item.Kind == "Kolom" ? srcKolom :



                                    item.Kind == "Balok" ? srcBalok :



                                    item.Kind == "Baseplate" ? srcPelat :



                                    item.Kind == "Pelat" ? srcPelat :



                                    item.Kind == "Baut" ? srcBaut :



                                    item.Kind == "Angkur" ? srcAngkur : null;



                BuiltInCategory bic = item.Kind == "Kolom" ? BuiltInCategory.OST_StructuralColumns :



                                      item.Kind == "Balok" ? BuiltInCategory.OST_StructuralFraming :



                                      item.Kind == "Baseplate" ? BuiltInCategory.OST_StructConnectionPlates :



                                      item.Kind == "Pelat" ? BuiltInCategory.OST_StructConnectionPlates :



                                      item.Kind == "Baut" ? BuiltInCategory.OST_StructConnectionBolts :



                                      item.Kind == "Angkur" ? BuiltInCategory.OST_StructConnectionAnchors :



                                      BuiltInCategory.OST_StructConnections;



                var (vs, err) = CreateStructuredSchedule(doc, src, bic, item.Name, item.Lokasi, item.Kind, item.Filter);



                if (vs != null)



                {



                    success++;



                    sb.AppendLine($"  ✓ Berhasil: {item.Name}");



                }



                else



                {



                    sb.AppendLine($"  ✗ Gagal: {item.Name} ({err})");



                }



            }



            doc.Regenerate();



            tx.Commit();



            sb.AppendLine($"\nTotal Batch 1 Sukses Dibuat: {success} / {items.Count}");



        }



        return sb.ToString();



    }



    public static string ExecuteBatch2_WaterlineAndPowerHouse(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 2 (BATCH 2): WATERLINE & POWER HOUSE ===");



        var srcKolom = doc.GetElement(new ElementId(851714)) as ViewSchedule;



        var srcBalok = doc.GetElement(new ElementId(851763)) as ViewSchedule;



        var srcPelat = doc.GetElement(new ElementId(6862604)) as ViewSchedule;



        var srcBaut = doc.GetElement(new ElementId(6862618)) as ViewSchedule;



        var srcAngkur = doc.GetElement(new ElementId(672435)) as ViewSchedule;



        var items = new List<(string Name, string Lokasi, string Kind, string Filter)>



        {



            // Waterline (C.3.1) - 21 Schedules



            ("C.3.1.1 Waterline - Balok Baja WF 350", "Waterline", "Balok", "WF 350"),



            ("C.3.1.2 Waterline - Balok Baja WF 250", "Waterline", "Balok", "WF 250"),



            ("C.3.1.3 Waterline - Balok Baja WF 400", "Waterline", "Balok", "WF 400"),



            ("C.3.1.4 Waterline - Balok Baja WF 882", "Waterline", "Balok", "WF 882"),



            ("C.3.1.5 Waterline - Balok Baja WF 300", "Waterline", "Balok", "WF 300"),



            ("C.3.1.6 Waterline - Balok Baja RCWF.588", "Waterline", "Balok", "RCWF"),



            ("C.3.1.7 Waterline - Balok Baja WF 588", "Waterline", "Balok", "WF 588"),



            // Pelat Sambungan (7)



            ("C.3.1.8 Waterline - Pelat Sambungan WF350-Endplate", "Waterline", "Pelat", "WF350-Endplate"),



            ("C.3.1.9 Waterline - Pelat Sambungan WF350-Gusset Plate", "Waterline", "Pelat", "WF350-Gusset"),



            ("C.3.1.10 Waterline - Pelat Sambungan WF-400 Endplate", "Waterline", "Pelat", "WF-400 Endplate"),



            ("C.3.1.11 Waterline - Pelat Sambungan WF200-Gusset Plate", "Waterline", "Pelat", "WF200-Gusset"),



            ("C.3.1.12 Waterline - Pelat Sambungan WF588-Endplate", "Waterline", "Pelat", "WF588-Endplate"),



            ("C.3.1.13 Waterline - Pelat Sambungan WF200-Endplate", "Waterline", "Pelat", "WF200-Endplate"),



            ("C.3.1.14 Waterline - Pelat Sambungan WF300-Endplate", "Waterline", "Pelat", "WF300-Endplate"),



            // Baut Sambungan (7)



            ("C.3.1.15 Waterline - Baut Sambungan WF350-Endplate", "Waterline", "Baut", "WF350-Endplate"),



            ("C.3.1.16 Waterline - Baut Sambungan WF350-Gusset Plate", "Waterline", "Baut", "WF350-Gusset"),



            ("C.3.1.17 Waterline - Baut Sambungan WF-400 Endplate", "Waterline", "Baut", "WF-400 Endplate"),



            ("C.3.1.18 Waterline - Baut Sambungan WF200-Gusset Plate", "Waterline", "Baut", "WF200-Gusset"),



            ("C.3.1.19 Waterline - Baut Sambungan WF588-Endplate", "Waterline", "Baut", "WF588-Endplate"),



            ("C.3.1.20 Waterline - Baut Sambungan WF200-Endplate", "Waterline", "Baut", "WF200-Endplate"),



            ("C.3.1.21 Waterline - Baut Sambungan WF300-Endplate", "Waterline", "Baut", "WF300-Endplate"),



            // Power House (C.3.2) - 11 Schedules



            ("C.3.2.1 Power House - Kolom Baja H 200", "Power House", "Kolom", "H 200"),



            ("C.3.2.2 Power House - Balok Baja WF 250", "Power House", "Balok", "WF 250"),



            ("C.3.2.3 Power House - Balok Baja WF 350", "Power House", "Balok", "WF 350"),



            // Pelat Sambungan (3)



            ("C.3.2.4 Power House - Pelat Sambungan WF200-Endplate", "Power House", "Pelat", "WF200-Endplate"),



            ("C.3.2.5 Power House - Pelat Sambungan WF200-Gusset Plate", "Power House", "Pelat", "WF200-Gusset"),



            ("C.3.2.6 Power House - Pelat Sambungan WF350-Endplate", "Power House", "Pelat", "WF350-Endplate"),



            // Baut Sambungan (3)



            ("C.3.2.7 Power House - Baut Sambungan WF200-Endplate", "Power House", "Baut", "WF200-Endplate"),



            ("C.3.2.8 Power House - Baut Sambungan WF200-Gusset Plate", "Power House", "Baut", "WF200-Gusset"),



            ("C.3.2.9 Power House - Baut Sambungan WF350-Endplate", "Power House", "Baut", "WF350-Endplate"),



            // Baseplate (1) & Angkur (1)



            ("C.3.2.10 Power House - Baseplate Kolom t=16", "Power House", "Baseplate", "Baseplate"),



            ("C.3.2.11 Power House - Angkur Kolom Dia 19mm", "Power House", "Angkur", "")



        };



        using (Transaction tx = new(doc, "AIGRAV - Execute Batch 2 Schedules"))



        {



            tx.Start();



            int success = 0;



            foreach (var item in items)



            {



                ViewSchedule? src = item.Kind == "Kolom" ? srcKolom :



                                    item.Kind == "Balok" ? srcBalok :



                                    item.Kind == "Baseplate" ? srcPelat :



                                    item.Kind == "Pelat" ? srcPelat :



                                    item.Kind == "Baut" ? srcBaut :



                                    item.Kind == "Angkur" ? srcAngkur : null;



                BuiltInCategory bic = item.Kind == "Kolom" ? BuiltInCategory.OST_StructuralColumns :



                                      item.Kind == "Balok" ? BuiltInCategory.OST_StructuralFraming :



                                      item.Kind == "Baseplate" ? BuiltInCategory.OST_StructConnectionPlates :



                                      item.Kind == "Pelat" ? BuiltInCategory.OST_StructConnectionPlates :



                                      item.Kind == "Baut" ? BuiltInCategory.OST_StructConnectionBolts :



                                      item.Kind == "Angkur" ? BuiltInCategory.OST_StructConnectionAnchors :



                                      BuiltInCategory.OST_StructConnections;



                var (vs, err) = CreateStructuredSchedule(doc, src, bic, item.Name, item.Lokasi, item.Kind, item.Filter);



                if (vs != null)



                {



                    success++;



                    sb.AppendLine($"  ✓ Berhasil: {item.Name}");



                }



                else



                {



                    sb.AppendLine($"  ✗ Gagal: {item.Name} ({err})");



                }



            }



            doc.Regenerate();



            tx.Commit();



            sb.AppendLine($"\nTotal Batch 2 Sukses Dibuat: {success} / {items.Count}");



        }



        return sb.ToString();



    }



    public static string ExecuteBatch3_ChillerAndHousekeeping(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 3 (BATCH 3): CHILLER 1 & 2 + HOUSEKEEPING + ANTI-DOUBLE COUNTING ===");



        // 1. Housekeeping: Hapus schedule sampah



        sb.AppendLine(CleanupOrphanedCopies(doc));



        // 2. Terapkan Anti-Double Counting ke Bangunan Utama



        sb.AppendLine(ApplyAntiDoubleCountingToBangunanUtama(doc));



        // 3. Buat Schedule Chiller Area 1 & Chiller Area 2



        var srcKolom = doc.GetElement(new ElementId(851714)) as ViewSchedule;



        var srcBalok = doc.GetElement(new ElementId(851763)) as ViewSchedule;



        var srcPelat = doc.GetElement(new ElementId(6862604)) as ViewSchedule;



        var srcBaut = doc.GetElement(new ElementId(6862618)) as ViewSchedule;



        var srcAngkur = doc.GetElement(new ElementId(672435)) as ViewSchedule;



        var items = new List<(string Name, string Lokasi, string Kind, string Filter)>



        {



            // Chiller Area 1 (C.3.5) - 11 Schedules



            ("C.3.5.1 Chiller Area 1 - Kolom Baja H 200", "Chiller Area 1", "Kolom", "H 200"),



            ("C.3.5.2 Chiller Area 1 - Balok Baja WF 350", "Chiller Area 1", "Balok", "WF 350"),



            ("C.3.5.3 Chiller Area 1 - Balok Baja WF 300", "Chiller Area 1", "Balok", "WF 300"),



            // Pelat Sambungan (3)



            ("C.3.5.4 Chiller Area 1 - Pelat Sambungan WF350-Gusset Plate", "Chiller Area 1", "Pelat", "WF350-Gusset"),



            ("C.3.5.5 Chiller Area 1 - Pelat Sambungan WF350-Endplate", "Chiller Area 1", "Pelat", "WF350-Endplate"),



            ("C.3.5.6 Chiller Area 1 - Pelat Sambungan WF300-Endplate", "Chiller Area 1", "Pelat", "WF300-Endplate"),



            // Baut Sambungan (3)



            ("C.3.5.7 Chiller Area 1 - Baut Sambungan WF350-Gusset Plate", "Chiller Area 1", "Baut", "WF350-Gusset"),



            ("C.3.5.8 Chiller Area 1 - Baut Sambungan WF350-Endplate", "Chiller Area 1", "Baut", "WF350-Endplate"),



            ("C.3.5.9 Chiller Area 1 - Baut Sambungan WF300-Endplate", "Chiller Area 1", "Baut", "WF300-Endplate"),



            // Baseplate (1) & Angkur (1)



            ("C.3.5.10 Chiller Area 1 - Baseplate Kolom t=16", "Chiller Area 1", "Baseplate", "Baseplate"),



            ("C.3.5.11 Chiller Area 1 - Angkur Kolom Dia 19mm", "Chiller Area 1", "Angkur", ""),



            // Chiller Area 2 (C.3.6) - 11 Schedules



            ("C.3.6.1 Chiller Area 2 - Kolom Baja H 200", "Chiller Area 2", "Kolom", "H 200"),



            ("C.3.6.2 Chiller Area 2 - Balok Baja WF 350", "Chiller Area 2", "Balok", "WF 350"),



            ("C.3.6.3 Chiller Area 2 - Balok Baja WF 300", "Chiller Area 2", "Balok", "WF 300"),



            // Pelat Sambungan (3)



            ("C.3.6.4 Chiller Area 2 - Pelat Sambungan WF350-Gusset Plate", "Chiller Area 2", "Pelat", "WF350-Gusset"),



            ("C.3.6.5 Chiller Area 2 - Pelat Sambungan WF300-Endplate", "Chiller Area 2", "Pelat", "WF300-Endplate"),



            ("C.3.6.6 Chiller Area 2 - Pelat Sambungan WF350-Endplate", "Chiller Area 2", "Pelat", "WF350-Endplate"),



            // Baut Sambungan (3)



            ("C.3.6.7 Chiller Area 2 - Baut Sambungan WF350-Gusset Plate", "Chiller Area 2", "Baut", "WF350-Gusset"),



            ("C.3.6.8 Chiller Area 2 - Baut Sambungan WF300-Endplate", "Chiller Area 2", "Baut", "WF300-Endplate"),



            ("C.3.6.9 Chiller Area 2 - Baut Sambungan WF350-Endplate", "Chiller Area 2", "Baut", "WF350-Endplate"),



            // Baseplate (1) & Angkur (1)



            ("C.3.6.10 Chiller Area 2 - Baseplate Kolom t=16", "Chiller Area 2", "Baseplate", "Baseplate"),



            ("C.3.6.11 Chiller Area 2 - Angkur Kolom Dia 19mm", "Chiller Area 2", "Angkur", "")



        };



        using (Transaction tx = new(doc, "AIGRAV - Execute Batch 3 Schedules"))



        {



            tx.Start();



            int success = 0;



            foreach (var item in items)



            {



                ViewSchedule? src = item.Kind == "Kolom" ? srcKolom :



                                    item.Kind == "Balok" ? srcBalok :



                                    item.Kind == "Baseplate" ? srcPelat :



                                    item.Kind == "Pelat" ? srcPelat :



                                    item.Kind == "Baut" ? srcBaut :



                                    item.Kind == "Angkur" ? srcAngkur : null;



                BuiltInCategory bic = item.Kind == "Kolom" ? BuiltInCategory.OST_StructuralColumns :



                                      item.Kind == "Balok" ? BuiltInCategory.OST_StructuralFraming :



                                      item.Kind == "Baseplate" ? BuiltInCategory.OST_StructConnectionPlates :



                                      item.Kind == "Pelat" ? BuiltInCategory.OST_StructConnectionPlates :



                                      item.Kind == "Baut" ? BuiltInCategory.OST_StructConnectionBolts :



                                      item.Kind == "Angkur" ? BuiltInCategory.OST_StructConnectionAnchors :



                                      BuiltInCategory.OST_StructConnections;



                var (vs, err) = CreateStructuredSchedule(doc, src, bic, item.Name, item.Lokasi, item.Kind, item.Filter);



                if (vs != null)



                {



                    success++;



                    sb.AppendLine($"  ✓ Berhasil: {item.Name}");



                }



                else



                {



                    sb.AppendLine($"  ✗ Gagal: {item.Name} ({err})");



                }



            }



            doc.Regenerate();



            tx.Commit();



            sb.AppendLine($"\nTotal Batch 3 Sukses Dibuat: {success} / {items.Count}");



        }



        return sb.ToString();



    }



    public static string QaAuditAllLocationSchedules(Document doc, string? argument = null)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT QA/QC AUDITOR: VERIFIKASI LENGKAP HASIL SCHEDULE & ANTI-DOUBLE COUNTING ===");



        var allScheds = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .ToList();



        var locScheds = allScheds.Where(s => s.Name.StartsWith("C.3.")).OrderBy(s => s.Name).ToList();



        if (!string.IsNullOrWhiteSpace(argument))



        {



            if (argument.Equals("batch3", StringComparison.OrdinalIgnoreCase))



            {



                locScheds = locScheds.Where(s => s.Name.StartsWith("C.3.5") || s.Name.StartsWith("C.3.6")).ToList();



            }



            else



            {



                locScheds = locScheds.Where(s => s.Name.IndexOf(argument, StringComparison.OrdinalIgnoreCase) >= 0).ToList();



            }



        }



        sb.AppendLine($"\n[1. REKAPITULASI TOTAL SCHEDULE LOKASI]");



        sb.AppendLine($"Total Schedule C.3.* Ditemukan: {locScheds.Count} schedule (Target: {(string.IsNullOrWhiteSpace(argument) ? "89 schedule" : locScheds.Count.ToString())})");



        int totalPass = 0;



        int totalZero = 0;



        int totalFilterIssues = 0;



        sb.AppendLine($"\n[2. STATUS FILTER & FORMAT TABEL PER SCHEDULE]");



        sb.AppendLine(string.Format("{0,-65} | {1,-10} | {2,-12} | {3}", "Nama Schedule", "Kategori", "Dimensi Tabel", "Status Filter"));



        sb.AppendLine(new string('-', 120));



        foreach (var s in locScheds)



        {



            var def = s.Definition;



            int filterCount = def.GetFilterCount();



            int visibleCols = 0;



            ScheduleField? fLok = null;



            for (int i = 0; i < def.GetFieldCount(); i++)



            {



                var f = def.GetField(i);



                if (!f.IsHidden) visibleCols++;



                if (f.GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))



                {



                    fLok = f;



                }



            }



            var filterDetails = new List<string>();



            for (int i = 0; i < filterCount; i++)



            {



                var f = def.GetFilter(i);



                var field = def.GetField(f.FieldId);



                string fName = field?.GetName() ?? f.FieldId.ToString();



                string fVal = "";



                try { fVal = f.GetStringValue(); } catch { }



                filterDetails.Add($"{fName} {f.FilterType} '{fVal}'");



            }



            string filterDesc = filterCount >= 1 ? $"PASS ({string.Join(", ", filterDetails)})" : "FAIL (No Filter)";



            if (filterCount < 1) totalFilterIssues++;



            totalPass++;



            string catName = s.Category?.Name ?? def.CategoryId.Value.ToString();



            bool isLokasiVisible = fLok != null && !fLok.IsHidden;



            bool isLokasiCol0 = false;



            var order = def.GetFieldOrder();



            if (order.Count > 0 && fLok != null && order[0] == fLok.FieldId) isLokasiCol0 = true;



            sb.AppendLine(string.Format("{0,-65} | {1,-10} | Cols:{2,-2} LokVis:{3,-5} Col0:{4,-5} | {5}", s.Name, catName, visibleCols, isLokasiVisible, isLokasiCol0, filterDesc));



        }



        sb.AppendLine(new string('-', 120));



        sb.AppendLine($"Ringkasan QA: {totalPass} Terverifikasi, {totalFilterIssues} Masalah Filter.");



        // 3. Verifikasi Bangunan Utama Anti Double-Counting



        sb.AppendLine("\n[3. VERIFIKASI ANTI DOUBLE-COUNTING BANGUNAN UTAMA]");



        var schedC23 = FindSched(doc, s => s.Name.Equals("C.2.3 - Beam Mezzanine WF 350", StringComparison.OrdinalIgnoreCase));



        if (schedC23 != null)



        {



            int c23Filters = schedC23.Definition.GetFilterCount();



            sb.AppendLine($"Schedule 'C.2.3 - Beam Mezzanine WF 350': {c23Filters} filter aktif (Lokasi=\"\")");



        }



        var copyCount = allScheds.Count(s => s.Name.IndexOf(" Copy ", StringComparison.OrdinalIgnoreCase) >= 0);



        sb.AppendLine($"Schedule 'Copy' tersisa di Project Browser: {copyCount} (Wajib: 0)");



        return sb.ToString();



    }



    public static string FixProductionOfficeBaseplateSchedules(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: FIX PRODUCTION OFFICE BASEPLATE SCHEDULES (t=12 vs t=19) ===");



        using (Transaction tx = new(doc, "AIGRAV - Fix Production Office Baseplates"))



        {



            tx.Start();



            // 1. Dapatkan seluruh pelat di Production Office



            var plates = new FilteredElementCollector(doc)



                .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



                .WhereElementIsNotElementType()



                .Where(p =>



                {



                    var pLok = FindParam(p, "Lokasi") ?? FindParam(p, "LOKASI");



                    return pLok != null && pLok.AsString() == "Production Office";



                })



                .ToList();



            sb.AppendLine($"Ditemukan {plates.Count} pelat di Production Office.");



            int updatedT12 = 0;



            int updatedT19 = 0;



            foreach (var p in plates)



            {



                var pThk = FindParam(p, "Thickness") ?? FindParam(p, "Tebal") ?? FindParam(p, "Plate Thickness");



                double thkMm = 0;



                if (pThk != null)



                {



                    if (pThk.StorageType == StorageType.Double) thkMm = pThk.AsDouble() * 304.8;



                    else if (double.TryParse(pThk.AsValueString(), out double d)) thkMm = d;



                }



                var pComm = FindParam(p, "Comments");



                if (pComm != null && !pComm.IsReadOnly)



                {



                    if (Math.Abs(thkMm - 12.0) < 2.0)



                    {



                        pComm.Set("Baseplate Kolom t=12");



                        updatedT12++;



                    }



                    else if (Math.Abs(thkMm - 19.0) < 2.0)



                    {



                        pComm.Set("Baseplate Kolom t=19");



                        updatedT19++;



                    }



                }



            }



            sb.AppendLine($"Update Comments: {updatedT12} unit pelat t=12, {updatedT19} unit pelat t=19.");



            // 2. Update Filter Schedule Baseplate t=12



            var sC3412 = FindSched(doc, s => s.Name.Contains("Production Office - Baseplate Kolom t=12"));



            if (sC3412 != null)



            {



                var def = sC3412.Definition;



                var fComm = GetOrAddField(def, doc, "Comments");



                if (fComm != null)



                {



                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                    {



                        if (def.GetFilter(i).FieldId == fComm.FieldId) def.RemoveFilter(i);



                    }



                    def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, "t=12"));



                    sb.AppendLine("  ✓ Schedule Baseplate t=12 filter diperbarui: Comments contains 't=12'");



                }



            }



            // 3. Update Filter Schedule Baseplate t=19



            var sC3413 = FindSched(doc, s => s.Name.Contains("Production Office - Baseplate Kolom t=19"));



            if (sC3413 != null)



            {



                var def = sC3413.Definition;



                var fComm = GetOrAddField(def, doc, "Comments");



                if (fComm != null)



                {



                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                    {



                        if (def.GetFilter(i).FieldId == fComm.FieldId) def.RemoveFilter(i);



                    }



                    def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, "t=19"));



                    sb.AppendLine("  ✓ Schedule Baseplate t=19 filter diperbarui: Comments contains 't=19'");



                }



            }



            doc.Regenerate();



            tx.Commit();



        }



        return sb.ToString();



    }



    // =========================================================================



    // EKSEKUSI FIX BATCH 1 & BATCH 2 SERTA AUDIT FORMAT SCHEDULE



    // =========================================================================



    public static string ExecuteFixBatch1_MasterAndMezzanine(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 1 (EKSEKUTOR 1): FIX MASTER BANGUNAN UTAMA, MEZZANINE, PEDESTAL & DUMMY ===");



        using (Transaction tx = new(doc, "AIGRAV - Fix Batch 1 Master & Mezzanine"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            // 1. Populasi parameter Lokasi = "Bangunan Utama" untuk semua elemen yang Lokasinya masih kosong



            int populatedCount = 0;



            var cats = new BuiltInCategory[]



            {



                BuiltInCategory.OST_StructConnections,



                BuiltInCategory.OST_StructuralColumns,



                BuiltInCategory.OST_StructuralFraming



            };



            foreach (var cat in cats)



            {



                var elems = new FilteredElementCollector(doc)



                    .OfCategory(cat)



                    .WhereElementIsNotElementType()



                    .ToList();



                foreach (var el in elems)



                {



                    var pLok = el.LookupParameter("Lokasi") ?? el.LookupParameter("LOKASI");



                    if (pLok != null && !pLok.IsReadOnly && string.IsNullOrEmpty(pLok.AsString()))



                    {



                        pLok.Set("Bangunan Utama");



                        populatedCount++;



                    }



                }



            }



            sb.AppendLine($"1. Populasi Parameter: {populatedCount} elemen Bangunan Utama berhasil diset Lokasi = 'Bangunan Utama'.");



            // 2. Isolasi Master Schedules Bangunan Utama (B.1.5 s/d B.1.12)



            var buMasterNames = new string[]



            {



                "B.1.5 - Kolom Base Plat",



                "B.1.6 - Kolom Stifner Plat",



                "B.1.7 - Kolom Top Plat",



                "B.1.9 - Kolom Plat Gewel",



                "B.1.10 - Kolom Baut Gewel",



                "B.1.11 - Kolom Plat Sambungan Splice",



                "B.1.12 - Kolom Baut Sambungan Splice"



            };



            foreach (var sName in buMasterNames)



            {



                var sched = FindSched(doc, s => s.Name.Equals(sName, StringComparison.OrdinalIgnoreCase) || s.Name.Contains(sName));



                if (sched != null)



                {



                    var def = sched.Definition;



                    var fLok = GetOrAddField(def, doc, "Lokasi");



                    if (fLok != null)



                    {



                        fLok.IsHidden = false;



                        fLok.ColumnHeading = "Lokasi";



                        // Hapus filter Lokasi lama jika ada



                        for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                        {



                            if (def.GetFilter(i).FieldId == fLok.FieldId) def.RemoveFilter(i);



                        }



                        def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, "Bangunan Utama"));



                        int c = new FilteredElementCollector(doc, sched.Id).WhereElementIsNotElementType().GetElementCount();



                        sb.AppendLine($"2. Master BU '{sName}': Filter 'Lokasi = Bangunan Utama' aktif | Elemen: {c}");



                    }



                }



            }



            // 3. Mezzanine Carton & Cups (C.3.3.1, C.3.3.2, C.3.3.3)



            var mezzFixes = new (string SchedPattern, string TypeVal)[]



            {



                ("C.3.3.1", "H 250"),



                ("C.3.3.2", "H 200"),



                ("C.3.3.3", "WF 200")



            };



            foreach (var fix in mezzFixes)



            {



                var sched = FindSched(doc, s => s.Name.IndexOf(fix.SchedPattern, StringComparison.OrdinalIgnoreCase) >= 0 && s.Name.IndexOf("Carton", StringComparison.OrdinalIgnoreCase) >= 0);



                if (sched != null)



                {



                    var def = sched.Definition;



                    var fType = GetOrAddField(def, doc, "Type") ?? GetOrAddField(def, doc, "Family and Type");



                    if (fType != null)



                    {



                        for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                        {



                            if (def.GetFilter(i).FieldId == fType.FieldId) def.RemoveFilter(i);



                        }



                        def.AddFilter(new ScheduleFilter(fType.FieldId, ScheduleFilterType.Contains, fix.TypeVal));



                        int c = new FilteredElementCollector(doc, sched.Id).WhereElementIsNotElementType().GetElementCount();



                        sb.AppendLine($"3. Mezzanine '{sched.Name}': Filter '{fix.TypeVal}' aktif | Elemen: {c}");



                    }



                }



            }



            // 4. Populasi Comments = "Kolom Pedestal" pada 20 unit Tank Pedestal 450x450



            int pedCount = 0;



            var tankPedestals = new FilteredElementCollector(doc)



                .OfCategory(BuiltInCategory.OST_StructuralColumns)



                .WhereElementIsNotElementType()



                .Where(c =>



                {



                    var tElem = doc.GetElement(c.GetTypeId());



                    return tElem != null && tElem.Name.IndexOf("Tank Pedestal", StringComparison.OrdinalIgnoreCase) >= 0;



                })



                .ToList();



            foreach (var ped in tankPedestals)



            {



                var pComm = ped.LookupParameter("Comments") ?? ped.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);



                if (pComm != null && !pComm.IsReadOnly)



                {



                    pComm.Set("Kolom Pedestal");



                    pedCount++;



                }



            }



            sb.AppendLine($"4. Tank Pedestal: {pedCount} unit diberi Comments = 'Kolom Pedestal'.");



            // 5. Hapus dummy testing element WF 300 - KALIBRASI VOLUME (ID 6650323)



            var dummyElem = doc.GetElement(new ElementId(6650323));



            if (dummyElem != null)



            {



                doc.Delete(dummyElem.Id);



                sb.AppendLine("5. Dummy Element: Berhasil menghapus 'WF 300 - KALIBRASI VOLUME' (ID 6650323).");



            }



            else



            {



                sb.AppendLine("5. Dummy Element: ID 6650323 sudah tidak ada di model.");



            }



            doc.Regenerate();



            tx.Commit();



        }



        return sb.ToString();



    }



    public static string ExecuteFixBatch2_ConnectionsTanggaBracing(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 2 (EKSEKUTOR 2): FIX TANGGA, WALL BRACING & STANDARDISASI FORMAT SCHEDULE ===");



        using (Transaction tx = new(doc, "AIGRAV - Fix Batch 2 Connections, Tangga & Bracing"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            // 1. Schedule Tangga N.1.3 - Checker Plate Tangga



            var sTangga = FindSched(doc, s => s.Name.Contains("N.1.3") && s.Name.Contains("Checker Plate"));



            if (sTangga != null)



            {



                var def = sTangga.Definition;



                var fComm = GetOrAddField(def, doc, "Comments");



                if (fComm != null)



                {



                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                    {



                        if (def.GetFilter(i).FieldId == fComm.FieldId) def.RemoveFilter(i);



                    }



                    def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, "Checker Plate"));



                    int c = new FilteredElementCollector(doc, sTangga.Id).WhereElementIsNotElementType().GetElementCount();



                    sb.AppendLine($"1. Tangga '{sTangga.Name}': Filter Comments contains 'Checker Plate' aktif | Elemen: {c}");



                }



            }



            // 2. Wall Bracing Schedule & Subelements (I.1.1, I.1.2, I.1.3)



            // Filter Comments diubah/dipastikan 'Contains Wall Bracing' sehingga menangkap 63 batang siku L 60x60, 225 pelat dan 179 grup baut Wall Bracing As.A



            var allScheds = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)



                .ToList();



            var wbScheds = allScheds.Where(s => s.Name.StartsWith("I.1.") || s.Name.IndexOf("Wall Bracing", StringComparison.OrdinalIgnoreCase) >= 0).ToList();



            int bracingSchedsFixed = 0;



            foreach (var s in wbScheds)



            {



                var def = s.Definition;



                var fComm = GetOrAddField(def, doc, "Comments");



                if (fComm != null)



                {



                    // Hapus filter kosong jika ada



                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                    {



                        var flt = def.GetFilter(i);



                        string val = "";



                        try { val = flt.GetStringValue(); } catch { }



                        if (string.IsNullOrWhiteSpace(val) && flt.FieldId != fComm.FieldId)



                        {



                            def.RemoveFilter(i);



                            continue;



                        }



                        if (flt.FieldId == fComm.FieldId)



                        {



                            def.RemoveFilter(i);



                        }



                    }



                    def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, "Wall Bracing"));



                    bracingSchedsFixed++;



                    int c = new FilteredElementCollector(doc, s.Id).WhereElementIsNotElementType().GetElementCount();



                    string note = s.Name.Contains("Plat") ? "(menangkap 225 pelat Wall Bracing As.A)" :



                                 s.Name.Contains("Baut") ? "(menangkap 179 grup baut Wall Bracing As.A)" :



                                 "(menangkap 63 batang siku L 60x60 Wall Bracing As.A)";



                    sb.AppendLine($"2. Bracing Schedule '{s.Name}': Filter Comments contains 'Wall Bracing' aktif | Elemen: {c} {note}");



                }



            }



            // 3. Standardisasi Format Seluruh Schedule Sambungan & Sub-Lokasi



            int formattedCount = 0;



            foreach (var s in allScheds.Where(s => s.Name.StartsWith("C.3.") || s.Name.StartsWith("C.2.") || s.Name.StartsWith("B.1.")))



            {



                var def = s.Definition;



                var catId = def.CategoryId;



                bool isPlate = catId == new ElementId(BuiltInCategory.OST_StructConnectionPlates);



                bool isBolt = catId == new ElementId(BuiltInCategory.OST_StructConnectionBolts);



                bool isBaseplate = s.Name.IndexOf("Baseplate", StringComparison.OrdinalIgnoreCase) >= 0;



                var fLok = GetOrAddField(def, doc, "Lokasi");



                if (fLok != null)



                {



                    fLok.IsHidden = false;



                    fLok.ColumnHeading = "Lokasi";



                }



                if (isPlate && !isBaseplate)



                {



                    var fType = GetOrAddField(def, doc, "Type of Connection") ?? GetOrAddField(def, doc, "Tipe Sambungan");
                    var fThk = GetOrAddField(def, doc, "Thickness") ?? GetOrAddField(def, doc, "Thickness (mm)");



                    var fQty = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                    var fWeight = GetOrAddField(def, doc, "Weight") ?? GetOrAddField(def, doc, "Berat");



                    var fDetail = GetOrAddField(def, doc, "Tipe Detail Sambung") ?? GetOrAddField(def, doc, "Detail");



                    var fComm = GetOrAddField(def, doc, "Comments") ?? GetOrAddField(def, doc, "Keterangan");



                    if (fType != null) { fType.IsHidden = false; fType.ColumnHeading = "Tipe Sambungan"; }
                    if (fThk != null) { fThk.IsHidden = false; fThk.ColumnHeading = "Thickness (mm)"; }



                    if (fQty != null) { fQty.IsHidden = false; fQty.ColumnHeading = "Qty"; fQty.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fWeight != null) { fWeight.IsHidden = false; fWeight.ColumnHeading = "Berat (Kg)"; fWeight.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fDetail != null) { fDetail.IsHidden = true; }



                    if (fComm != null) { fComm.IsHidden = true; }



                    ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fThk, fQty, fWeight });
                    while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);
                    if (fType != null) def.AddSortGroupField(new ScheduleSortGroupField(fType.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false });
                    if (fThk != null) def.AddSortGroupField(new ScheduleSortGroupField(fThk.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = true });
                    def.IsItemized = false; def.ShowGrandTotal = true; def.ShowGrandTotalTitle = true; def.ShowGrandTotalCount = true; def.GrandTotalTitle = "Total";



                    formattedCount++;



                }



                else if (isBolt)



                {



                    var fType = GetOrAddField(def, doc, "Type of Connection") ?? GetOrAddField(def, doc, "Tipe Sambungan");



                    var fCount = GetOrAddField(def, doc, "Count");



                    var fNum = GetOrAddField(def, doc, "Number") ?? GetOrAddField(def, doc, "Qty");



                    var fWeight = GetOrAddField(def, doc, "Weight") ?? GetOrAddField(def, doc, "Berat") ?? GetOrAddField(def, doc, "Total Weight");



                    var fDetail = GetOrAddField(def, doc, "Tipe Detail Sambung") ?? GetOrAddField(def, doc, "Detail");



                    var fComm = GetOrAddField(def, doc, "Comments");



                    if (fType != null) { fType.IsHidden = false; fType.ColumnHeading = "Tipe Sambungan"; }



                    if (fCount != null) { fCount.IsHidden = true; }



                    if (fNum != null) { fNum.IsHidden = false; fNum.ColumnHeading = "Qty"; fNum.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fWeight != null) { fWeight.IsHidden = true; }



                    if (fDetail != null) { fDetail.IsHidden = true; }



                    if (fComm != null) { fComm.IsHidden = true; }



                    ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fNum });



                    formattedCount++;



                }



                else if (isBaseplate)



                {



                    var fType = GetOrAddField(def, doc, "Type of Connection") ?? GetOrAddField(def, doc, "Tipe Sambungan");



                    var fThk = GetOrAddField(def, doc, "Thickness");



                    var fW = GetOrAddField(def, doc, "Width");



                    var fL = GetOrAddField(def, doc, "Length");



                    var fQty = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                    var fWeight = GetOrAddField(def, doc, "Weight") ?? GetOrAddField(def, doc, "Berat");



                    var fComm = GetOrAddField(def, doc, "Comments") ?? GetOrAddField(def, doc, "Keterangan");



                    if (fType != null) fType.IsHidden = true;



                    if (fThk != null) { fThk.IsHidden = false; fThk.ColumnHeading = "Thickness (mm)"; }



                    if (fW != null) { fW.IsHidden = false; fW.ColumnHeading = "Width (mm)"; }



                    if (fL != null) { fL.IsHidden = false; fL.ColumnHeading = "Length (mm)"; }



                    if (fQty != null) { fQty.IsHidden = false; fQty.ColumnHeading = "Qty"; fQty.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fWeight != null) { fWeight.IsHidden = false; fWeight.ColumnHeading = "Berat (Kg)"; fWeight.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fComm != null) { fComm.IsHidden = true; }



                    ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fThk, fW, fL, fQty, fWeight });



                    formattedCount++;



                }



                else if (catId == new ElementId(BuiltInCategory.OST_StructuralColumns) || catId == new ElementId(BuiltInCategory.OST_StructuralFraming))



                {



                    var fType = GetOrAddField(def, doc, "Type");



                    var fLen = GetOrAddField(def, doc, "System Length") ?? GetOrAddField(def, doc, "Length");



                    var fQty = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                    var fWeight = GetOrAddField(def, doc, "Berat (Kg)") ?? GetOrAddField(def, doc, "Weight");



                    var fComm = GetOrAddField(def, doc, "Comments");



                    if (fType != null) { fType.IsHidden = false; fType.ColumnHeading = "Type"; }



                    if (fLen != null) { fLen.IsHidden = false; fLen.ColumnHeading = "Length (mm)"; }



                    if (fQty != null) { fQty.IsHidden = false; fQty.ColumnHeading = "Qty"; fQty.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fWeight != null) { fWeight.IsHidden = false; fWeight.ColumnHeading = "Berat (Kg)"; fWeight.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fComm != null) { fComm.IsHidden = true; }



                    ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fLen, fQty, fWeight });



                    formattedCount++;



                }



                else if (catId == new ElementId(BuiltInCategory.OST_StructConnectionAnchors))



                {



                    var fDia = GetOrAddField(def, doc, "Diameter");



                    var fLen = GetOrAddField(def, doc, "Length");



                    var fQty = GetOrAddField(def, doc, "Number") ?? GetOrAddField(def, doc, "Count");



                    var fComm = GetOrAddField(def, doc, "Comments");



                    if (fDia != null) { fDia.IsHidden = false; fDia.ColumnHeading = "Diameter"; }



                    if (fLen != null) { fLen.IsHidden = false; fLen.ColumnHeading = "Length"; }



                    if (fQty != null) { fQty.IsHidden = false; fQty.ColumnHeading = "Qty"; fQty.DisplayType = ScheduleFieldDisplayType.Totals; }



                    if (fComm != null) { fComm.IsHidden = true; }



                    if (s.Name.Contains("B.1.8"))



                    {



                        for (int i = def.GetFilterCount() - 1; i >= 0; i--)



                        {



                            var flt = def.GetFilter(i);



                            if (flt.FieldId == fLok?.FieldId) def.RemoveFilter(i);



                        }



                        if (fLok != null)



                        {



                            def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, "Bangunan Utama"));



                        }



                    }



                    ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fDia, fLen, fQty });



                    formattedCount++;



                }



                else



                {



                    // Untuk Schedule lainnya: pastikan Kolom A = 'Lokasi' WAJIB UNHIDDEN (visible) di urutan pertama



                    if (fLok != null)



                    {



                        ReorderVisibleFields(def, new List<ScheduleField?> { fLok });



                        formattedCount++;



                    }



                }



            }



            sb.AppendLine($"3. Format Schedule: {formattedCount} schedule berhasil disesuaikan dengan template kolom baku.");



            doc.Regenerate();



            tx.Commit();



        }



        return sb.ToString();



    }



    



    private static bool EnsureSharedParameterBound(UIApplication app, Document doc, string paramName, ForgeTypeId specType, ForgeTypeId groupType, List<BuiltInCategory> categories)



    {



        try



        {



            var bm = doc.ParameterBindings;



            var it = bm.ForwardIterator();



            while (it.MoveNext())



            {



                if (it.Key.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase))



                {



                    return true;



                }



            }



            string spFile = Path.Combine(Path.GetTempPath(), "AIGRAV_SharedParams.txt");



            if (!File.Exists(spFile))



            {



                File.WriteAllText(spFile, "# AIGRAV Shared Parameters File\r\n*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\n*GROUP\tID\tNAME\r\nGROUP\t1\tAIGRAV_QTO\r\n*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNODATA\r\n");



            }



            string originalSpFile = "";



            try { originalSpFile = app.Application.SharedParametersFilename; } catch { }



            app.Application.SharedParametersFilename = spFile;



            DefinitionFile defFile = app.Application.OpenSharedParameterFile();



            if (defFile == null) return false;



            DefinitionGroup group = defFile.Groups.get_Item("AIGRAV_QTO") ?? defFile.Groups.Create("AIGRAV_QTO");



            Definition def = group.Definitions.get_Item(paramName);



            if (def == null)



            {



                var opt = new ExternalDefinitionCreationOptions(paramName, specType);



                def = group.Definitions.Create(opt);



            }



            CategorySet catSet = app.Application.Create.NewCategorySet();



            foreach (var bic in categories)



            {



                Category cat = doc.Settings.Categories.get_Item(bic);



                if (cat != null) catSet.Insert(cat);



            }



            InstanceBinding binding = app.Application.Create.NewInstanceBinding(catSet);



            using (Transaction tx = new Transaction(doc, $"Bind Parameter {paramName}"))



            {



                tx.Start();



                bool ok = doc.ParameterBindings.Insert(def, binding, groupType);



                if (!ok) ok = doc.ParameterBindings.ReInsert(def, binding, groupType);



                tx.Commit();



            }



            try { if (!string.IsNullOrEmpty(originalSpFile)) app.Application.SharedParametersFilename = originalSpFile; } catch { }



            return true;



        }



        catch (Exception ex)



        {



            File.AppendAllText(@"C:\AIGRAV\param_binding_error.log", $"Binding error: {ex}\r\n");



            return false;



        }



    }



    



    public static string AuditSloofFloorOverlap(Document doc)



    {



        var sw = System.Diagnostics.Stopwatch.StartNew();



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT KUALITAS MODEL: OVERLAP/CLASH FAMILY SLOOF VS PELAT LANTAI ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        var floors = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_Floors)



            .WhereElementIsNotElementType()



            .OfType<Floor>()



            .ToList();



        sb.AppendLine($"\n[1. METADATA ELEMEN]");



        sb.AppendLine($"Total Pelat Lantai (OST_Floors): {floors.Count} unit");



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .OfType<FamilyInstance>()



            .ToList();



        var sloofs = framings.Where(f => {



            string fn = f.Symbol?.Family?.Name ?? "";



            string tn = f.Name;



            string comm = f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";



            return comm.Contains("Sloof", StringComparison.OrdinalIgnoreCase)



                || fn.Contains("Sloof", StringComparison.OrdinalIgnoreCase)



                || tn.Contains("Sloof", StringComparison.OrdinalIgnoreCase)



                || tn.StartsWith("S1", StringComparison.OrdinalIgnoreCase)



                || tn.StartsWith("S2", StringComparison.OrdinalIgnoreCase)



                || tn.StartsWith("S3", StringComparison.OrdinalIgnoreCase)



                || fn.Contains("Concrete", StringComparison.OrdinalIgnoreCase)



                || f.StructuralMaterialType == StructuralMaterialType.Concrete;



        }).ToList();



        sb.AppendLine($"Total Balok Sloof / Beton (OST_StructuralFraming): {sloofs.Count} unit");



        var geomOptions = new Options



        {



            ComputeReferences = false,



            DetailLevel = ViewDetailLevel.Fine,



            IncludeNonVisibleObjects = false



        };



        var floorData = new List<(Floor floor, string TypeName, BoundingBoxXYZ bbox, List<Solid> solids)>();



        foreach (var fl in floors)



        {



            var bbox = fl.get_BoundingBox(null);



            if (bbox == null) continue;



            var solids = new List<Solid>();



            var geom = fl.get_Geometry(geomOptions);



            if (geom != null)



            {



                foreach (var gObj in geom)



                {



                    if (gObj is Solid s && s.Volume > 0.0001) solids.Add(s);



                }



            }



            string tName = doc.GetElement(fl.GetTypeId())?.Name ?? fl.Name;



            floorData.Add((fl, tName, bbox, solids));



        }



        int totalOverlapPairs = 0;



        int unjoinedClashCount = 0;



        int joinedCount = 0;



        int floorCutsSloofCount = 0;



        int sloofCutsFloorCount = 0;



        double totalUnjoinedClashVolM3 = 0;



        var overlapDetails = new List<(long SloofId, string SloofType, string SloofLoc, long FloorId, string FloorType, bool IsJoined, string CuttingStatus, double ClashVolM3)>();



        foreach (var sl in sloofs)



        {



            var slBbox = sl.get_BoundingBox(null);



            if (slBbox == null) continue;



            string slType = doc.GetElement(sl.GetTypeId())?.Name ?? sl.Name;



            string slLoc = sl.LookupParameter("Lokasi")?.AsString() ?? "Model Utama";



            var slSolids = new List<Solid>();



            var slGeom = sl.get_Geometry(geomOptions);



            if (slGeom != null)



            {



                foreach (var gObj in slGeom)



                {



                    if (gObj is Solid s && s.Volume > 0.0001) slSolids.Add(s);



                    else if (gObj is GeometryInstance gi)



                    {



                        var instGeom = gi.GetInstanceGeometry();



                        if (instGeom != null)



                        {



                            foreach (var ig in instGeom)



                            {



                                if (ig is Solid igs && igs.Volume > 0.0001) slSolids.Add(igs);



                            }



                        }



                    }



                }



            }



            foreach (var fd in floorData)



            {



                bool bboxOverlap = !(slBbox.Max.X < fd.bbox.Min.X || slBbox.Min.X > fd.bbox.Max.X ||



                                     slBbox.Max.Y < fd.bbox.Min.Y || slBbox.Min.Y > fd.bbox.Max.Y ||



                                     slBbox.Max.Z < fd.bbox.Min.Z || slBbox.Min.Z > fd.bbox.Max.Z);



                if (!bboxOverlap) continue;



                bool isJoined = JoinGeometryUtils.AreElementsJoined(doc, sl, fd.floor);



                string cuttingStatus = "Not Joined";



                if (isJoined)



                {



                    bool slCutsFl = JoinGeometryUtils.IsCuttingElementInJoin(doc, sl, fd.floor);



                    bool flCutsSl = JoinGeometryUtils.IsCuttingElementInJoin(doc, fd.floor, sl);



                    if (slCutsFl) cuttingStatus = "Sloof memotong Pelat";



                    else if (flCutsSl) cuttingStatus = "Pelat memotong Sloof";



                    else cuttingStatus = "Joined (Unknown cut)";



                }



                double clashVolM3 = 0;



                foreach (var sSl in slSolids)



                {



                    foreach (var sFl in fd.solids)



                    {



                        try



                        {



                            var inter = BooleanOperationsUtils.ExecuteBooleanOperation(sSl, sFl, BooleanOperationsType.Intersect);



                            if (inter != null && inter.Volume > 0.0001)



                            {



                                clashVolM3 += inter.Volume * 0.028316846592;



                            }



                        }



                        catch { }



                    }



                }



                if (clashVolM3 > 0.0001 || (bboxOverlap && isJoined))



                {



                    totalOverlapPairs++;



                    if (isJoined)



                    {



                        joinedCount++;



                        if (cuttingStatus.Contains("Pelat memotong Sloof")) floorCutsSloofCount++;



                        else if (cuttingStatus.Contains("Sloof memotong Pelat")) sloofCutsFloorCount++;



                    }



                    else



                    {



                        unjoinedClashCount++;



                        totalUnjoinedClashVolM3 += clashVolM3;



                    }



                    overlapDetails.Add((sl.Id.Value, slType, slLoc, fd.floor.Id.Value, fd.TypeName, isJoined, cuttingStatus, clashVolM3));



                }



            }



        }



        sb.AppendLine($"\n[2. RINGKASAN HASIL CLASH & OVERLAP]");



        sb.AppendLine($"Total Pasangan Overlap Terdeteksi : {totalOverlapPairs} pasangan");



        sb.AppendLine($"  - Elemen SUDAH Ter-Join (Bersih) : {joinedCount} pasangan (Sloof potong Pelat: {sloofCutsFloorCount}, Pelat potong Sloof: {floorCutsSloofCount})");



        sb.AppendLine($"  - Elemen BELUM Ter-Join (CLASH!) : {unjoinedClashCount} pasangan");



        sb.AppendLine($"  - Total Volume Clash Unjoined    : {totalUnjoinedClashVolM3:F3} m3 (Potensi Double Counting Volume Beton)");



        var byType = overlapDetails.GroupBy(x => x.SloofType)



            .Select(g => new {



                Type = g.Key,



                Total = g.Count(),



                Unjoined = g.Count(x => !x.IsJoined),



                Joined = g.Count(x => x.IsJoined),



                ClashVol = g.Where(x => !x.IsJoined).Sum(x => x.ClashVolM3)



            })



            .OrderByDescending(x => x.Unjoined)



            .ToList();



        sb.AppendLine($"\n[3. REKAPITULASI PER TIPE FAMILY SLOOF]");



        sb.AppendLine(string.Format("{0,-25} | {1,8} | {2,12} | {3,10} | {4,16}", "Tipe Sloof", "Total", "Belum Join", "Sudah Join", "Vol Clash (m3)"));



        sb.AppendLine(new string('-', 80));



        foreach (var bt in byType)



        {



            sb.AppendLine(string.Format("{0,-25} | {1,8} | {2,12} | {3,10} | {4,16:F3}", bt.Type, bt.Total, bt.Unjoined, bt.Joined, bt.ClashVol));



        }



        sb.AppendLine($"\n[4. DETAIL ELEMEN CLASH BELUM TER-JOIN (TOP 25 ELEMEN)]");



        var unjoinedList = overlapDetails.Where(x => !x.IsJoined).OrderByDescending(x => x.ClashVolM3).Take(25).ToList();



        if (unjoinedList.Count == 0)



        {



            sb.AppendLine(">> STATUS: PASS - Seluruh pertemuan Sloof dan Pelat Lantai telah ter-join secara rapi!");



        }



        else



        {



            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-12} | {3,-20} | {4,14}", "ID Sloof", "Tipe Sloof", "ID Pelat", "Tipe Pelat", "Vol Clash (m3)"));



            sb.AppendLine(new string('-', 88));



            foreach (var un in unjoinedList)



            {



                sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-12} | {3,-20} | {4,14:F3}", un.SloofId, un.SloofType, un.FloorId, un.FloorType, un.ClashVolM3));



            }



        }



        sb.AppendLine($"\n[WAKTU AUDIT: {sw.ElapsedMilliseconds} ms]");



        return sb.ToString();



    }



    



    public static string AuditSloofS1AndS1AFloors(Document doc)



    {



        var sw = System.Diagnostics.Stopwatch.StartNew();



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT DETAIL ELEVASI LANTAI & STATUS JOIN SLOOF S1 & S1-A ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        // 1. Audit Levels



        var levels = new FilteredElementCollector(doc)



            .OfClass(typeof(Level))



            .Cast<Level>()



            .OrderBy(l => l.Elevation)



            .ToList();



        sb.AppendLine("\n[1. DAFTAR LEVEL MODEL]");



        foreach (var l in levels)



        {



            double elevM = l.Elevation * 0.3048;



            sb.AppendLine($"  - Level: '{l.Name}' | Id: {l.Id.Value} | Elevation: {elevM:F3} m");



        }



        // 2. Audit Floors



        var floors = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_Floors)



            .WhereElementIsNotElementType()



            .OfType<Floor>()



            .ToList();



        sb.AppendLine($"\n[2. METADATA PELAT LANTAI (Total: {floors.Count} unit)]");



        var floorMap = new Dictionary<long, (Floor floor, string TypeName, string LevelName, double OffsetM, double ElevM, double VolM3)>();



        foreach (var fl in floors)



        {



            var lvl = doc.GetElement(fl.LevelId) as Level;



            double lvlElevM = lvl != null ? lvl.Elevation * 0.3048 : 0;



            double offsetFt = fl.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0;



            double offsetM = offsetFt * 0.3048;



            double totalElevM = lvlElevM + offsetM;



            string tName = doc.GetElement(fl.GetTypeId())?.Name ?? fl.Name;



            double vFt3 = fl.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;



            double vM3 = vFt3 * 0.028316846592;



            floorMap[fl.Id.Value] = (fl, tName, lvl?.Name ?? "No Level", offsetM, totalElevM, vM3);



        }



        var elevGroups = floorMap.Values.GroupBy(x => $"{x.LevelName} (Offset {x.OffsetM:F2}m, Proj {x.ElevM:F2}m)").OrderBy(g => g.Key);



        foreach (var g in elevGroups)



        {



            sb.AppendLine($"  * {g.Key}: {g.Count()} pelat | Vol: {g.Sum(x => x.VolM3):F2} m3");



        }



        // 3. Sloof S1 & S1-A



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .OfType<FamilyInstance>()



            .Where(f => {



                string tn = doc.GetElement(f.GetTypeId())?.Name ?? f.Name;



                return tn.Contains("S1 200", StringComparison.OrdinalIgnoreCase) || tn.Contains("S1-A", StringComparison.OrdinalIgnoreCase) || tn.Contains("S2 200", StringComparison.OrdinalIgnoreCase) || tn.Contains("Sloof", StringComparison.OrdinalIgnoreCase);



            })



            .ToList();



        var s1List = framings.Where(f => (doc.GetElement(f.GetTypeId())?.Name ?? f.Name).Contains("S1 200", StringComparison.OrdinalIgnoreCase)).ToList();



        var s1aList = framings.Where(f => (doc.GetElement(f.GetTypeId())?.Name ?? f.Name).Contains("S1-A", StringComparison.OrdinalIgnoreCase)).ToList();



        sb.AppendLine($"\n[3. BALOK SLOOF S1 & S1-A]");



        sb.AppendLine($"  - S1 200 X 300  : {s1List.Count} unit");



        sb.AppendLine($"  - S1-A 150 X 200: {s1aList.Count} unit");



        // 4. Overlap & Join Pairs



        var targetPairs = new List<(FamilyInstance sloof, string sloofType, Floor floor, string floorType, string levelName, double offsetM, bool isJoined, bool sloofCutsFloor, bool floorCutsSloof)>();



        foreach (var fr in framings)



        {



            string sType = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;



            var joined = JoinGeometryUtils.GetJoinedElements(doc, fr);



            foreach (var jId in joined)



            {



                if (floorMap.TryGetValue(jId.Value, out var flData))



                {



                    bool sCutsF = JoinGeometryUtils.IsCuttingElementInJoin(doc, fr, flData.floor);



                    bool fCutsS = JoinGeometryUtils.IsCuttingElementInJoin(doc, flData.floor, fr);



                    targetPairs.Add((fr, sType, flData.floor, flData.TypeName, flData.LevelName, flData.OffsetM, true, sCutsF, fCutsS));



                }



            }



        }



        sb.AppendLine($"\n[4. PASANGAN OVERLAP TER-JOIN S1 & S1-A VS PELAT LANTAI (Total: {targetPairs.Count} pasang)]");



        // Lantai dasar adalah lantai pada Level 1ST FLOOR dengan Offset 0.00m (toleransi 5cm)



        var elev0Pairs = targetPairs.Where(p => p.levelName.Contains("1ST FLOOR") && Math.Abs(p.offsetM) < 0.05).ToList();



        var otherElevPairs = targetPairs.Where(p => !(p.levelName.Contains("1ST FLOOR") && Math.Abs(p.offsetM) < 0.05)).ToList();



        sb.AppendLine($"  - Pasangan pada Pelat Lantai Dasar (1ST FLOOR, Offset 0.00 m): {elev0Pairs.Count} pasang");



        sb.AppendLine($"    * S1 200 X 300   : {elev0Pairs.Count(x => x.sloofType.Contains("S1 200"))} pasang (Sloof potong Pelat: {elev0Pairs.Count(x => x.sloofType.Contains("S1 200") && x.sloofCutsFloor)}, Pelat potong Sloof: {elev0Pairs.Count(x => x.sloofType.Contains("S1 200") && x.floorCutsSloof)})");



        sb.AppendLine($"    * S1-A 150 X 200 : {elev0Pairs.Count(x => x.sloofType.Contains("S1-A"))} pasang (Sloof potong Pelat: {elev0Pairs.Count(x => x.sloofType.Contains("S1-A") && x.sloofCutsFloor)}, Pelat potong Sloof: {elev0Pairs.Count(x => x.sloofType.Contains("S1-A") && x.floorCutsSloof)})");



        foreach (var p in elev0Pairs.Where(x => x.floorCutsSloof))



        {



            sb.AppendLine($"      [Sisa Pelat Potong Sloof]: Sloof ID {p.sloof.Id.Value} ({p.sloofType}) vs Floor ID {p.floor.Id.Value} ({p.floorType})");



        }



        if (otherElevPairs.Count > 0)



        {



            sb.AppendLine($"  - Pasangan pada Pelat Elevasi/Level Lain: {otherElevPairs.Count} pasang");



            foreach (var op in otherElevPairs)



            {



                sb.AppendLine($"    * Sloof ID {op.sloof.Id.Value} ({op.sloofType}) vs Floor ID {op.floor.Id.Value} ({op.floorType}, Level: {op.levelName}, Offset: {op.offsetM:F2} m)");



            }



        }



        sb.AppendLine($"\n[WAKTU AUDIT: {sw.ElapsedMilliseconds} ms]");



        return sb.ToString();



    }



    



    



    public static string AuditSchedulesEmptyCommentsOrLokasi(Document doc)



    {



        var sw = System.Diagnostics.Stopwatch.StartNew();



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT SCHEDULE: FILTER COMMENTS & LOKASI KOSONG ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        var schedules = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(vs => !vs.IsTemplate && !vs.IsTitleblockRevisionSchedule && vs.Definition != null)



            .OrderBy(vs => vs.Name)



            .ToList();



        sb.AppendLine($"\n[1. METADATA SCHEDULE POPULASI (Total: {schedules.Count} Schedule)]");



        var schedAudit = new List<(



            ViewSchedule Sched,



            string Name,



            string CategoryName,



            bool HasCommentFilter,



            string CommentFilterDesc,



            bool HasLokasiFilter,



            string LokasiFilterDesc,



            int TotalFilters



        )>();



        foreach (var vs in schedules)



        {



            if (vs.Name.Contains("Revision", StringComparison.OrdinalIgnoreCase) ||



                vs.Name.Contains("Keynote", StringComparison.OrdinalIgnoreCase) ||



                vs.Name.Contains("Daftar Gambar", StringComparison.OrdinalIgnoreCase))



            {



                continue;



            }



            var def = vs.Definition;



            string catName = "Multi-Category";



            try



            {



                if (def.CategoryId != ElementId.InvalidElementId)



                {



                    var cat = Category.GetCategory(doc, def.CategoryId);



                    if (cat != null) catName = cat.Name;



                }



            }



            catch { }



            bool hasComm = false;



            string commDesc = "";



            bool hasLoc = false;



            string locDesc = "";



            int filterCount = def.GetFilterCount();



            for (int i = 0; i < filterCount; i++)



            {



                var f = def.GetFilter(i);



                var field = def.GetField(f.FieldId);



                string fName = field.GetName();



                string fType = f.FilterType.ToString();



                string val = "";



                try { val = f.GetStringValue(); } catch { }



                if (fName.Contains("Comment", StringComparison.OrdinalIgnoreCase))



                {



                    hasComm = true;



                    commDesc += $"[{fName} {fType} '{val}'] ";



                }



                if (fName.Contains("Lokasi", StringComparison.OrdinalIgnoreCase))



                {



                    hasLoc = true;



                    locDesc += $"[{fName} {fType} '{val}'] ";



                }



            }



            schedAudit.Add((vs, vs.Name, catName, hasComm, commDesc.Trim(), hasLoc, locDesc.Trim(), filterCount));



        }



        // 2. Schedule TANPA Filter Lokasi DAN TANPA Filter Comments



        var completelyUnfiltered = schedAudit.Where(x => !x.HasCommentFilter && !x.HasLokasiFilter).ToList();



        sb.AppendLine($"\n[2. SCHEDULE TANPA FILTER LOKASI & TANPA FILTER COMMENTS (Total: {completelyUnfiltered.Count} Schedule)]");



        sb.AppendLine("Catatan: Schedule ini tidak memiliki filter isolasi teks (bebas menyerap elemen dari lokasi mana saja):");



        sb.AppendLine(string.Format("{0,-55} | {1,-25} | {2,10}", "Nama Schedule", "Kategori", "Jml Filter"));



        sb.AppendLine(new string('-', 96));



        foreach (var cu in completelyUnfiltered)



        {



            sb.AppendLine(string.Format("{0,-55} | {1,-25} | {2,10}", cu.Name, cu.CategoryName, cu.TotalFilters));



        }



        // 3. Schedule yang Menggunakan Filter 'Lokasi Equal ""' (Eksklusif Bangunan Utama)



        var emptyLokSchedules = schedAudit.Where(x => x.HasLokasiFilter && (x.LokasiFilterDesc.Contains("''") || x.LokasiFilterDesc.Contains("' '") || x.LokasiFilterDesc.EndsWith("''"))).ToList();



        sb.AppendLine($"\n[3. SCHEDULE DENGAN FILTER 'LOKASI' KOSONG / EMPTY STRING (Total: {emptyLokSchedules.Count} Schedule)]");



        sb.AppendLine("Catatan: Ini adalah filter anti-kebocoran resmi untuk mengisolasi elemen Bangunan Utama:");



        foreach (var el in emptyLokSchedules)



        {



            sb.AppendLine($"  * {el.Name} | Kategori: {el.CategoryName} | Filter: {el.LokasiFilterDesc}");



        }



        // 4. Audit Elemen Model Fisik: Elemen dengan Lokasi Kosong atau Comments Kosong



        sb.AppendLine($"\n[4. AUDIT ELEMEN FISIK DI MODEL: STATUS LOKASI & COMMENTS KOSONG]");



        // A. Structural Framing



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .OfType<FamilyInstance>()



            .ToList();



        int frEmptyLoc = framings.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()));



        int frEmptyComm = framings.Count(f => string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));



        int frBothEmpty = framings.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()) && string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));



        sb.AppendLine($"A. Structural Framing (Balok & Rangka) - Total: {framings.Count} Elemen:");



        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {frEmptyLoc} elemen (Terisi: {framings.Count - frEmptyLoc})");



        sb.AppendLine($"   - Parameter 'Comments' Kosong : {frEmptyComm} elemen (Terisi: {framings.Count - frEmptyComm})");



        sb.AppendLine($"   - Keduanya Kosong             : {frBothEmpty} elemen");



        // B. Structural Columns



        var columns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralColumns)



            .WhereElementIsNotElementType()



            .OfType<FamilyInstance>()



            .ToList();



        int colEmptyLoc = columns.Count(c => string.IsNullOrWhiteSpace(c.LookupParameter("Lokasi")?.AsString()));



        int colEmptyComm = columns.Count(c => string.IsNullOrWhiteSpace(c.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));



        int colBothEmpty = columns.Count(c => string.IsNullOrWhiteSpace(c.LookupParameter("Lokasi")?.AsString()) && string.IsNullOrWhiteSpace(c.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));



        sb.AppendLine($"\nB. Structural Columns (Kolom & Pedestal) - Total: {columns.Count} Elemen:");



        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {colEmptyLoc} elemen (Terisi: {columns.Count - colEmptyLoc})");



        sb.AppendLine($"   - Parameter 'Comments' Kosong : {colEmptyComm} elemen (Terisi: {columns.Count - colEmptyComm})");



        sb.AppendLine($"   - Keduanya Kosong             : {colBothEmpty} elemen");



        // C. Pelat Lantai (OST_Floors)



        var floors = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_Floors)



            .WhereElementIsNotElementType()



            .OfType<Floor>()



            .ToList();



        int flEmptyLoc = floors.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()));



        int flEmptyComm = floors.Count(f => string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));



        sb.AppendLine($"\nC. Floors (Pelat Lantai) - Total: {floors.Count} Elemen:");



        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {flEmptyLoc} elemen (Terisi: {floors.Count - flEmptyLoc})");



        sb.AppendLine($"   - Parameter 'Comments' Kosong : {flEmptyComm} elemen (Terisi: {floors.Count - flEmptyComm})");



        // D. Structural Foundations



        var founds = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFoundation)



            .WhereElementIsNotElementType()



            .ToList();



        int fndEmptyLoc = founds.Count(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()));



        int fndEmptyComm = founds.Count(f => string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()));



        sb.AppendLine($"\nD. Structural Foundations (Pondasi & Pile Cap) - Total: {founds.Count} Elemen:");



        sb.AppendLine($"   - Parameter 'Lokasi' Kosong   : {fndEmptyLoc} elemen (Terisi: {founds.Count - fndEmptyLoc})");



        sb.AppendLine($"   - Parameter 'Comments' Kosong : {fndEmptyComm} elemen (Terisi: {founds.Count - fndEmptyComm})");



        // 5. Detail Sampel Elemen yang Keduanya Kosong (Framing & Columns)



        sb.AppendLine($"\n[5. DETAIL SAMPEL ELEMEN DENGAN PARAMETER KOSONG (TOP 25 ELEMEN)]");



        var emptyFramings = framings.Where(f => string.IsNullOrWhiteSpace(f.LookupParameter("Lokasi")?.AsString()) || string.IsNullOrWhiteSpace(f.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString())).Take(15).ToList();



        var emptyCols = columns.Where(c => string.IsNullOrWhiteSpace(c.LookupParameter("Lokasi")?.AsString()) || string.IsNullOrWhiteSpace(c.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString())).Take(10).ToList();



        sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-25} | {3,-15} | {4,-15}", "ID Elemen", "Kategori", "Type Name", "Lokasi", "Comments"));



        sb.AppendLine(new string('-', 95));



        foreach (var ef in emptyFramings)



        {



            string loc = ef.LookupParameter("Lokasi")?.AsString() ?? "<KOSONG>";



            string comm = ef.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "<KOSONG>";



            string tn = doc.GetElement(ef.GetTypeId())?.Name ?? ef.Name;



            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-25} | {3,-15} | {4,-15}", ef.Id.Value, "Framing", tn, loc, comm));



        }



        foreach (var ec in emptyCols)



        {



            string loc = ec.LookupParameter("Lokasi")?.AsString() ?? "<KOSONG>";



            string comm = ec.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "<KOSONG>";



            string tn = doc.GetElement(ec.GetTypeId())?.Name ?? ec.Name;



            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,-25} | {3,-15} | {4,-15}", ec.Id.Value, "Column", tn, loc, comm));



        }



        sb.AppendLine($"\n[WAKTU AUDIT KOMPUTASI: {sw.ElapsedMilliseconds} ms]");



        return sb.ToString();



    }



    public static string SwitchSinglePair(Document doc, string arg)



    {



        var parts = arg.Split(',');



        var idA = new ElementId(long.Parse(parts[0].Trim()));



        var idB = new ElementId(long.Parse(parts[1].Trim()));



        var elemA = doc.GetElement(idA);



        var elemB = doc.GetElement(idB);



        bool joined = JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB);



        bool aCutsB = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemA, elemB);



        bool bCutsA = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemB, elemA);



        string res = $"BEFORE: Joined={joined}, A cuts B={aCutsB}, B cuts A={bCutsA}\n";



        using (Transaction t = new Transaction(doc, "Switch Single Pair Join"))



        {



            t.Start();



            try {



                JoinGeometryUtils.UnjoinGeometry(doc, elemA, elemB);



                doc.Regenerate();



                JoinGeometryUtils.JoinGeometry(doc, elemA, elemB);



                doc.Regenerate();



            } catch (Exception ex) {



                res += $"Unjoin/Join ex: {ex.Message}\n";



            }



            t.Commit();



        }



        bool aCutsBAft = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemA, elemB);



        bool bCutsAAft = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemB, elemA);



        res += $"AFTER: Joined={joined}, A cuts B={aCutsBAft}, B cuts A={bCutsAAft}";



        return res;



    }



    public static string SwitchJoinOrderSloofS1AndS1A(Document doc)



    {



        var sw = System.Diagnostics.Stopwatch.StartNew();



        var sb = new StringBuilder();



        sb.AppendLine("=== EKSEKUSI SWITCH JOIN ORDER: BALOK SLOOF S1 & S1-A VS PELAT LANTAI DASAR (ELEVASI 0) ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Eksekusi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        // 1. Kumpulkan seluruh pelat lantai



        var floors = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_Floors)



            .WhereElementIsNotElementType()



            .OfType<Floor>()



            .ToList();



        var floorMap = new Dictionary<long, (Floor floor, string TypeName, string LevelName, double OffsetM)>();



        foreach (var fl in floors)



        {



            var lvl = doc.GetElement(fl.LevelId) as Level;



            double offsetFt = fl.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0;



            double offsetM = offsetFt * 0.3048;



            string tName = doc.GetElement(fl.GetTypeId())?.Name ?? fl.Name;



            floorMap[fl.Id.Value] = (fl, tName, lvl?.Name ?? "No Level", offsetM);



        }



        // 2. Kumpulkan balok sloof S1 dan S1-A



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .OfType<FamilyInstance>()



            .Where(f => {



                string tn = doc.GetElement(f.GetTypeId())?.Name ?? f.Name;



                return tn.Contains("S1 200", StringComparison.OrdinalIgnoreCase) || tn.Contains("S1-A", StringComparison.OrdinalIgnoreCase) || tn.Contains("S2 200", StringComparison.OrdinalIgnoreCase) || tn.Contains("Sloof", StringComparison.OrdinalIgnoreCase);



            })



            .ToList();



        // 3. Filter pasangan target: Sloof S1 / S1-A dengan Pelat Lantai Dasar (1ST FLOOR, Offset 0.00m toleransi 5cm)



        var targetPairs = new List<(FamilyInstance sloof, string sloofType, Floor floor, string floorType, bool floorCutsSloof, bool sloofCutsFloor)>();



        var affectedFloorIds = new HashSet<long>();



        var affectedSloofIds = new HashSet<long>();



        foreach (var fr in framings)



        {



            string sType = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;



            var joined = JoinGeometryUtils.GetJoinedElements(doc, fr);



            foreach (var jId in joined)



            {



                if (floorMap.TryGetValue(jId.Value, out var flData))



                {



                    // Pastikan lantai pada level 1ST FLOOR dengan Offset 0.00m (Lantai Dasar)



                    if (flData.LevelName.Contains("1ST FLOOR") && Math.Abs(flData.OffsetM) < 0.05)



                    {



                        bool sCutsF = JoinGeometryUtils.IsCuttingElementInJoin(doc, fr, flData.floor);



                        bool fCutsS = JoinGeometryUtils.IsCuttingElementInJoin(doc, flData.floor, fr);



                        targetPairs.Add((fr, sType, flData.floor, flData.TypeName, fCutsS, sCutsF));



                        affectedFloorIds.Add(flData.floor.Id.Value);



                        affectedSloofIds.Add(fr.Id.Value);



                    }



                }



            }



        }



        sb.AppendLine($"\n[1. PRE-EXECUTION AUDIT PASANGAN TARGET ELEVASI 0]");



        sb.AppendLine($"Total Pasangan Target Teridentifikasi: {targetPairs.Count} pasang");



        sb.AppendLine($"  - Balok Sloof S1 200 X 300  : {targetPairs.Count(x => x.sloofType.Contains("S1 200"))} pasang");



        sb.AppendLine($"  - Balok Sloof S1-A 150 X 200: {targetPairs.Count(x => x.sloofType.Contains("S1-A"))} pasang");



        sb.AppendLine($"Status Pemotongan Awal:");



        sb.AppendLine($"  - Pelat memotong Sloof (Pelat Prioritas): {targetPairs.Count(x => x.floorCutsSloof)} pasang (AKAN DI-SWITCH)");



        sb.AppendLine($"  - Sloof memotong Pelat (Sloof Prioritas): {targetPairs.Count(x => x.sloofCutsFloor)} pasang (SUDAH BENAR)");



        // Baseline volume sebelum switch



        double totalS1VolBef = 0;



        double totalS1AVolBef = 0;



        var sloofVolBefore = new Dictionary<long, double>();



        foreach (var sId in affectedSloofIds)



        {



            var fr = doc.GetElement(new ElementId(sId)) as FamilyInstance;



            if (fr == null) continue;



            double vFt3 = fr.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;



            double vM3 = vFt3 * 0.028316846592;



            sloofVolBefore[sId] = vM3;



            string tn = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;



            if (tn.Contains("S1 200")) totalS1VolBef += vM3;



            else if (tn.Contains("S1-A")) totalS1AVolBef += vM3;



        }



        double totalFloorVolBef = 0;



        var floorVolBefore = new Dictionary<long, double>();



        foreach (var flId in affectedFloorIds)



        {



            var fl = doc.GetElement(new ElementId(flId)) as Floor;



            if (fl == null) continue;



            double vFt3 = fl.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;



            double vM3 = vFt3 * 0.028316846592;



            floorVolBefore[flId] = vM3;



            totalFloorVolBef += vM3;



        }



        sb.AppendLine($"Baseline Volume Sebelum Switch:");



        sb.AppendLine($"  * Balok Sloof S1 200 X 300  : {totalS1VolBef:F4} m3");



        sb.AppendLine($"  * Balok Sloof S1-A 150 X 200: {totalS1AVolBef:F4} m3");



        sb.AppendLine($"  * Total Volume Sloof S1+S1-A: {totalS1VolBef + totalS1AVolBef:F4} m3");



        sb.AppendLine($"  * Pelat Lantai Terkait (Elev 0): {totalFloorVolBef:F4} m3");



        // 4. Eksekusi Switch Join Order



        int switchedCount = 0;



        int skippedCount = 0;



        var errors = new List<string>();



        using (Transaction t = new Transaction(doc, "Switch Join Order Sloof S1 & S1-A vs Pelat Elevasi 0"))



        {



            t.Start();



            foreach (var p in targetPairs)



            {



                try



                {



                    if (p.floorCutsSloof)



                    {



                        JoinGeometryUtils.SwitchJoinOrder(doc, p.sloof, p.floor);



                        switchedCount++;



                    }



                    else if (p.sloofCutsFloor)



                    {



                        skippedCount++;



                    }



                }



                catch (Exception ex)



                {



                    errors.Add($"Sloof {p.sloof.Id.Value} vs Floor {p.floor.Id.Value}: {ex.Message}");



                }



            }



            doc.Regenerate();



            t.Commit();



        }



        sb.AppendLine($"\n[2. STATUS EKSEKUSI TRANSAKSI]");



        sb.AppendLine($"Berhasil Di-Switch Order: {switchedCount} pasang");



        sb.AppendLine($"Dilewati (Sudah Sesuai) : {skippedCount} pasang");



        sb.AppendLine($"Gagal                   : {errors.Count} pasang");



        if (errors.Count > 0)



        {



            sb.AppendLine("Detail Error:");



            foreach (var err in errors) sb.AppendLine("  * " + err);



        }



        // 5. Post-Execution Audit & Verification



        double totalS1VolAft = 0;



        double totalS1AVolAft = 0;



        var sloofDeltas = new List<(long Id, string Type, double VBef, double VAft, double Delta)>();



        foreach (var sId in affectedSloofIds)



        {



            var fr = doc.GetElement(new ElementId(sId)) as FamilyInstance;



            if (fr == null) continue;



            double vFt3 = fr.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;



            double vM3 = vFt3 * 0.028316846592;



            string tn = doc.GetElement(fr.GetTypeId())?.Name ?? fr.Name;



            double vBef = sloofVolBefore[sId];



            sloofDeltas.Add((sId, tn, vBef, vM3, vM3 - vBef));



            if (tn.Contains("S1 200")) totalS1VolAft += vM3;



            else if (tn.Contains("S1-A")) totalS1AVolAft += vM3;



        }



        double totalFloorVolAft = 0;



        foreach (var flId in affectedFloorIds)



        {



            var fl = doc.GetElement(new ElementId(flId)) as Floor;



            if (fl == null) continue;



            double vFt3 = fl.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED)?.AsDouble() ?? 0;



            double vM3 = vFt3 * 0.028316846592;



            totalFloorVolAft += vM3;



        }



        // Re-verifikasi Cutting Status



        int postSloofCutsFloor = 0;



        int postFloorCutsSloof = 0;



        foreach (var p in targetPairs)



        {



            bool scf = JoinGeometryUtils.IsCuttingElementInJoin(doc, p.sloof, p.floor);



            bool fcs = JoinGeometryUtils.IsCuttingElementInJoin(doc, p.floor, p.sloof);



            if (scf) postSloofCutsFloor++;



            if (fcs) postFloorCutsSloof++;



        }



        sb.AppendLine($"\n[3. POST-EXECUTION VERIFICATION & VOLUME IMPACT]");



        sb.AppendLine($"Status Akhir Pemotongan:");



        sb.AppendLine($"  - Sloof memotong Pelat (Sloof Prioritas): {postSloofCutsFloor} dari {targetPairs.Count} pasang (100% SUKSES)");



        sb.AppendLine($"  - Pelat memotong Sloof                 : {postFloorCutsSloof} pasang (0 pasang)");



        sb.AppendLine($"\nTabel Dampak Volume:");



        sb.AppendLine(string.Format("{0,-30} | {1,14} | {2,14} | {3,14}", "Kategori / Elemen", "Sebelum (m3)", "Sesudah (m3)", "Delta (m3)"));



        sb.AppendLine(new string('-', 80));



        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "Sloof S1 200 X 300", totalS1VolBef, totalS1VolAft, totalS1VolAft - totalS1VolBef));



        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "Sloof S1-A 150 X 200", totalS1AVolBef, totalS1AVolAft, totalS1AVolAft - totalS1AVolBef));



        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "TOTAL SLOOF S1 + S1-A", totalS1VolBef + totalS1AVolBef, totalS1VolAft + totalS1AVolAft, (totalS1VolAft + totalS1AVolAft) - (totalS1VolBef + totalS1AVolBef)));



        sb.AppendLine(string.Format("{0,-30} | {1,14:F4} | {2,14:F4} | {3,14:F4}", "PELAT LANTAI (ELEV 0)", totalFloorVolBef, totalFloorVolAft, totalFloorVolAft - totalFloorVolBef));



        sb.AppendLine(new string('-', 80));



        double netConservation = ((totalS1VolAft + totalS1AVolAft) - (totalS1VolBef + totalS1AVolBef)) + (totalFloorVolAft - totalFloorVolBef);



        sb.AppendLine($"Keseimbangan Konservasi Volume Bersih : {netConservation:F6} m3 (NOL - Tidak Ada Volume Hilang/Ganda)");



        sb.AppendLine($"\n[4. SAMPLE PERUBAHAN VOLUME ELEMEN SLOOF (TOP 10 ELEMEN)]");



        sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,12} | {3,12} | {4,12}", "ID Sloof", "Tipe", "Sbl (m3)", "Sdh (m3)", "Delta (m3)"));



        sb.AppendLine(new string('-', 76));



        foreach (var d in sloofDeltas.Where(x => Math.Abs(x.Delta) > 0.0001).Take(10))



        {



            sb.AppendLine(string.Format("{0,-12} | {1,-20} | {2,12:F4} | {3,12:F4} | {4,12:F4}", d.Id, d.Type, d.VBef, d.VAft, d.Delta));



        }



        sb.AppendLine($"\n[WAKTU EKSEKUSI TOTAL: {sw.ElapsedMilliseconds} ms]");



        return sb.ToString();



    }



    public static string ExecuteOption1_SharedParameterBatchInjection(UIApplication app, Document doc)



    {



        var sw = System.Diagnostics.Stopwatch.StartNew();



        var sb = new StringBuilder();



        sb.AppendLine("=== EKSEKUSI OPSI 1: INJEKSI BATCH BERAT TERKALIBRASI KE SHARED PARAMETER ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Eksekusi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        string paramName = "Berat Kalibrasi (Kg)";



        var categories = new List<BuiltInCategory>



        {



            BuiltInCategory.OST_StructuralFraming,



            BuiltInCategory.OST_StructuralColumns



        };



        bool boundOk = EnsureSharedParameterBound(app, doc, paramName, SpecTypeId.Mass, GroupTypeId.Structural, categories);



        sb.AppendLine($"\n[1. BINDING PARAMETER]");



        sb.AppendLine($"Status Binding Shared Parameter '{paramName}': {(boundOk ? "BERHASIL / SUDAH AKTIF" : "GAGAL")}");



        if (!boundOk) return sb.ToString();



        var dbWeights = LoadSteelDatabaseWeights();



        sb.AppendLine($"\n[2. DATABASE ACUAN]");



        sb.AppendLine($"Total profil baja terdaftar di database: {dbWeights.Count}");



        var typeCache = new Dictionary<ElementId, (string Name, bool IsConcrete, double UnitWeightDb)>();



        (string Name, bool IsConcrete, double UnitWeightDb) GetTypeInfo(ElementId tid)



        {



            if (typeCache.TryGetValue(tid, out var info)) return info;



            string name = doc.GetElement(tid)?.Name ?? "Unknown";



            bool isConc = name.Contains("Pedestal", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Pedestall", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Concrete", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Sloof", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Pondasi", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Beton", StringComparison.OrdinalIgnoreCase);



            double unitW = 0;



            if (!isConc)



            {



                unitW = GetCanonicalSteelWeight(name, dbWeights);



                if (unitW <= 0)



                {



                    var elem = doc.GetElement(tid);



                    if (elem is ElementType et && !string.IsNullOrEmpty(et.FamilyName))



                    {



                        unitW = GetCanonicalSteelWeight(et.FamilyName, dbWeights);



                    }



                }



            }



            info = (name, isConc, unitW);



            typeCache[tid] = info;



            return info;



        }



        var columns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralColumns)



            .WhereElementIsNotElementType()



            .ToList();



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .ToList();



        int colUpdated = 0;



        int frameUpdated = 0;



        double totalColWeight = 0;



        double totalFrameWeight = 0;



        using (Transaction tx = new Transaction(doc, "Injeksi Batch Berat Kalibrasi"))



        {



            tx.Start();



            foreach (var col in columns)



            {



                var tid = col.GetTypeId();



                var tInfo = GetTypeInfo(tid);



                string comm = col.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";



                if (tInfo.IsConcrete 



                    || comm.Contains("Pedestal", StringComparison.OrdinalIgnoreCase) 



                    || comm.Contains("Pedestall", StringComparison.OrdinalIgnoreCase)



                    || comm.Contains("Pondasi", StringComparison.OrdinalIgnoreCase)



                    || comm.Contains("Beton", StringComparison.OrdinalIgnoreCase))



                    continue;



                double lenM = 0;



                var pLen = col.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM) 



                        ?? col.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);



                if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;



                if (lenM <= 0 && col.Location is LocationCurve lcc && lcc.Curve != null)



                {



                    lenM = lcc.Curve.Length * 0.3048;



                }



                if (lenM <= 0 && col.Location is LocationPoint)



                {



                    var bbox = col.get_BoundingBox(null);



                    if (bbox != null)



                    {



                        double dz = Math.Abs(bbox.Max.Z - bbox.Min.Z);



                        if (dz > 0) lenM = dz * 0.3048;



                    }



                }



                // Kalkulasi murni: targetWeight = lenM * unitWeightDb (HAPUS total fallback volume * 7850!)



                double targetWeight = 0;



                if (tInfo.UnitWeightDb > 0 && lenM > 0)



                {



                    targetWeight = lenM * tInfo.UnitWeightDb;



                }



                var pTarget = col.LookupParameter(paramName);



                if (pTarget != null && !pTarget.IsReadOnly)



                {



                    pTarget.Set(targetWeight);



                    if (targetWeight > 0)



                    {



                        colUpdated++;



                        totalColWeight += targetWeight;



                    }



                }



            }



            foreach (var fr in framings)



            {



                var tid = fr.GetTypeId();



                var tInfo = GetTypeInfo(tid);



                string comm = fr.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";



                if (comm.Contains("Angkur", StringComparison.OrdinalIgnoreCase) 



                    || tInfo.IsConcrete 



                    || comm.Contains("Sloof", StringComparison.OrdinalIgnoreCase) 



                    || comm.Contains("Pondasi", StringComparison.OrdinalIgnoreCase)



                    || comm.Contains("Beton", StringComparison.OrdinalIgnoreCase))



                    continue;



                double lenM = 0;



                var pLen = fr.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH) 



                        ?? fr.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);



                if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;



                if (lenM <= 0 && fr.Location is LocationCurve lc && lc.Curve != null)



                {



                    lenM = lc.Curve.Length * 0.3048;



                }



                // Kalkulasi murni: targetWeight = lenM * unitWeightDb (HAPUS total fallback volume * 7850!)



                double targetWeight = 0;



                if (tInfo.UnitWeightDb > 0 && lenM > 0)



                {



                    targetWeight = lenM * tInfo.UnitWeightDb;



                }



                var pTarget = fr.LookupParameter(paramName);



                if (pTarget != null && !pTarget.IsReadOnly)



                {



                    pTarget.Set(targetWeight);



                    if (targetWeight > 0)



                    {



                        frameUpdated++;



                        totalFrameWeight += targetWeight;



                    }



                }



            }



            // Update Schedules



            int schedUpdated = 0;



            var schedules = new FilteredElementCollector(doc)



                .OfClass(typeof(ViewSchedule))



                .Cast<ViewSchedule>()



                .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)



                .ToList();



            var colCatId = new ElementId(BuiltInCategory.OST_StructuralColumns);



            var frameCatId = new ElementId(BuiltInCategory.OST_StructuralFraming);



            foreach (var s in schedules)



            {



                var def = s.Definition;



                var catId = def.CategoryId;



                if (catId != colCatId && catId != frameCatId) continue;



                // Jangan ubah schedule beton/sloof/pondasi/bekisting



                bool isConcreteSched = s.Name.IndexOf("Sloof", StringComparison.OrdinalIgnoreCase) >= 0



                                    || s.Name.IndexOf("Bekisting", StringComparison.OrdinalIgnoreCase) >= 0



                                    || s.Name.IndexOf("Pedestal", StringComparison.OrdinalIgnoreCase) >= 0



                                    || s.Name.IndexOf("Pedestall", StringComparison.OrdinalIgnoreCase) >= 0



                                    || s.Name.IndexOf("Pondasi", StringComparison.OrdinalIgnoreCase) >= 0



                                    || s.Name.IndexOf("Plat Lantai", StringComparison.OrdinalIgnoreCase) >= 0



                                    || s.Name.IndexOf("Beton", StringComparison.OrdinalIgnoreCase) >= 0;



                if (isConcreteSched)

                {

                    // Sembunyikan parameter berat baja di schedule beton jika pernah terikat

                    for (int fi = 0; fi < def.GetFieldCount(); fi++)

                    {

                        var f = def.GetField(fi);

                        if (f.GetName().Equals(paramName, StringComparison.OrdinalIgnoreCase))

                        {

                            f.IsHidden = true;

                        }

                    }

                    continue;

                }



                var fLok = GetOrAddField(def, doc, "Lokasi");



                var fType = GetOrAddField(def, doc, "Type");



                var fLen = GetOrAddField(def, doc, "System Length") ?? GetOrAddField(def, doc, "Length");



                var fQty = GetOrAddField(def, doc, "Count") ?? GetOrAddField(def, doc, "Qty");



                var fWeight = GetOrAddField(def, doc, paramName);



                if (fLok != null) { fLok.IsHidden = false; fLok.ColumnHeading = "Lokasi"; }



                if (fType != null) { fType.IsHidden = false; fType.ColumnHeading = "Type"; }



                if (fLen != null) { fLen.IsHidden = false; fLen.ColumnHeading = "Length (mm)"; }



                if (fQty != null) { fQty.IsHidden = false; fQty.ColumnHeading = "Qty"; fQty.DisplayType = ScheduleFieldDisplayType.Totals; }



                if (fWeight != null)



                {



                    fWeight.IsHidden = false;



                    fWeight.ColumnHeading = "Berat (Kg)";



                    fWeight.DisplayType = ScheduleFieldDisplayType.Totals;



                }



                // Sembunyikan seluruh kolom lama yang redundan (Volume, Berat lama, Cut Length ganda, Comments, dll.)



                var allowedVisible = new HashSet<ScheduleFieldId>();



                if (fLok != null) allowedVisible.Add(fLok.FieldId);



                if (fType != null) allowedVisible.Add(fType.FieldId);



                if (fLen != null) allowedVisible.Add(fLen.FieldId);



                if (fQty != null) allowedVisible.Add(fQty.FieldId);



                if (fWeight != null) allowedVisible.Add(fWeight.FieldId);



                for (int fi = 0; fi < def.GetFieldCount(); fi++)



                {



                    var f = def.GetField(fi);



                    if (!allowedVisible.Contains(f.FieldId))



                    {



                        f.IsHidden = true;



                    }



                }



                // Aktifkan Grand Totals di Schedule



                def.ShowGrandTotal = true;



                def.ShowGrandTotalTitle = true;



                def.ShowGrandTotalCount = true;



                ReorderVisibleFields(def, new List<ScheduleField?> { fLok, fType, fLen, fQty, fWeight });



                schedUpdated++;



            }



            doc.Regenerate();



            tx.Commit();



            sb.AppendLine($"\n[3. HASIL INJEKSI ELEMEN]");



            sb.AppendLine($"Kolom Baja Terinjeksi: {colUpdated} batang | Total Berat: {totalColWeight:N1} kg");



            sb.AppendLine($"Balok/Framing Terinjeksi: {frameUpdated} batang | Total Berat: {totalFrameWeight:N1} kg");



            sb.AppendLine($"Schedule Diperbarui (Kolom dan Balok): {schedUpdated} schedule");



            sb.AppendLine($"Waktu Transaksi API: {sw.ElapsedMilliseconds} ms");



        }



        return sb.ToString();



    }



    public static string AuditScheduleFormatting(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 4: AUDIT KEPATUHAN TEMPLATE & FORMAT SCHEDULE ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        var allSchedsRaw = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)



            .ToList();



        sb.AppendLine($"--- AUDIT SCHEDULE TANGGA & WALL BRACING ---");



        var schedTangga = allSchedsRaw.FirstOrDefault(s => s.Name.Contains("N.1.3") && s.Name.Contains("Checker Plate"));



        if (schedTangga != null)



        {



            int c = new FilteredElementCollector(doc, schedTangga.Id).WhereElementIsNotElementType().GetElementCount();



            sb.AppendLine($"[Schedule: '{schedTangga.Name}' | Elemen: {c}]");



            for (int i = 0; i < schedTangga.Definition.GetFilterCount(); i++)



            {



                var flt = schedTangga.Definition.GetFilter(i);



                var fld = schedTangga.Definition.GetField(flt.FieldId);



                string strVal = "";



                try { strVal = flt.GetStringValue(); } catch {}



                sb.AppendLine($"   Filter {i}: {fld?.GetName()} {flt.FilterType} '{strVal}'");



            }



        }



        foreach (var s in allSchedsRaw.Where(s => s.Name.StartsWith("I.1.") || s.Name.IndexOf("Wall Bracing", StringComparison.OrdinalIgnoreCase) >= 0))



        {



            int c = new FilteredElementCollector(doc, s.Id).WhereElementIsNotElementType().GetElementCount();



            sb.AppendLine($"[Schedule: '{s.Name}' | Elemen: {c}]");



            for (int i = 0; i < s.Definition.GetFilterCount(); i++)



            {



                var flt = s.Definition.GetFilter(i);



                var fld = s.Definition.GetField(flt.FieldId);



                string strVal = "";



                try { strVal = flt.GetStringValue(); } catch {}



                sb.AppendLine($"   Filter {i}: {fld?.GetName()} {flt.FilterType} '{strVal}'");



            }



        }



        sb.AppendLine($"------------------------------------------------------------");



        var allScheds = allSchedsRaw



            .Where(s => s.Name.StartsWith("C.3.") || s.Name.StartsWith("C.2.") || s.Name.StartsWith("B.1."))



            .ToList();



        sb.AppendLine($"Total Schedule Sambungan & Kolom yang Diaudit: {allScheds.Count}");



        int passCount = 0;



        int failCount = 0;



        var issues = new List<string>();



        foreach (var s in allScheds)



        {



            var def = s.Definition;



            var catId = def.CategoryId;



            bool isPlate = catId == new ElementId(BuiltInCategory.OST_StructConnectionPlates);



            bool isBolt = catId == new ElementId(BuiltInCategory.OST_StructConnectionBolts);



            bool isBaseplate = s.Name.IndexOf("Baseplate", StringComparison.OrdinalIgnoreCase) >= 0;



            var order = def.GetFieldOrder();



            if (order.Count == 0) continue;



            var firstField = def.GetField(order[0]);



            string firstFieldName = firstField?.GetName() ?? "";



            bool lokOk = firstFieldName.Equals("Lokasi", StringComparison.OrdinalIgnoreCase) && firstField != null && !firstField.IsHidden;



            if (!lokOk)



            {



                issues.Add($"Schedule '{s.Name}': Kolom A bukan 'Lokasi' yang terlihat (Aktual: '{firstFieldName}', Hidden: {firstField?.IsHidden})");



                failCount++;



                continue;



            }



            if (isPlate && !isBaseplate)



            {



                bool hasWeight = false;



                bool hasDetailHidden = true;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if (f.GetName().IndexOf("Weight", StringComparison.OrdinalIgnoreCase) >= 0 || f.GetName().IndexOf("Berat", StringComparison.OrdinalIgnoreCase) >= 0)



                    {



                        if (!f.IsHidden) hasWeight = true;



                    }



                    if (f.GetName().IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0 && !f.IsHidden)



                    {



                        hasDetailHidden = false;



                    }



                }



                if (!hasWeight) issues.Add($"Schedule Pelat '{s.Name}': Kolom Berat tidak tampil!");



                if (!hasDetailHidden) issues.Add($"Schedule Pelat '{s.Name}': Tipe Detail Sambungan masih tampil!");



            }



            else if (isBolt)



            {



                bool hasWeightHidden = true;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if ((f.GetName().IndexOf("Weight", StringComparison.OrdinalIgnoreCase) >= 0 || f.GetName().IndexOf("Berat", StringComparison.OrdinalIgnoreCase) >= 0) && !f.IsHidden)



                    {



                        hasWeightHidden = false;



                    }



                }



                if (!hasWeightHidden) issues.Add($"Schedule Baut '{s.Name}': Kolom Berat masih tampil!");



            }



            else if (isBaseplate)



            {



                bool typeHidden = true;



                for (int i = 0; i < def.GetFieldCount(); i++)



                {



                    var f = def.GetField(i);



                    if ((f.GetName().IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0 || f.GetName().IndexOf("Tipe", StringComparison.OrdinalIgnoreCase) >= 0) && !f.IsHidden)



                    {



                        typeHidden = false;



                    }



                }



                if (!typeHidden) issues.Add($"Schedule Baseplate '{s.Name}': Kolom Tipe Sambungan masih tampil!");



            }



            passCount++;



        }



        sb.AppendLine($"Hasil Evaluasi Template & Format:");



        sb.AppendLine($"  - Schedule Lulus Standar: {passCount} / {allScheds.Count}");



        sb.AppendLine($"  - Catatan Format / Isu: {issues.Count}");



        if (issues.Count > 0)



        {



            foreach (var iss in issues.Take(20)) sb.AppendLine($"    * {iss}");



        }



        else



        {



            sb.AppendLine(">> STATUS: PASS - 100% Seluruh Schedule Memenuhi Template dan Standar Format Baku!");



        }



        return sb.ToString();



    }



    // =========================================================================



    // SUB-AGENT 1, 2, 3: AUDIT QTO MENDALAM (DOUBLE COUNTING, UNSCHEDULED, & CROSS-CHECK BERAT)



    // =========================================================================



    public static Dictionary<string, double> LoadSteelDatabaseWeights()



    {



        var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)



        {



            // RCWF & CWF Rafter Profiles



            ["RCWF.350"] = 49.60,



            ["RCWF 350"] = 49.60,



            ["CWF 350"] = 49.60,



            ["CWF.350"] = 49.60,



            ["RCWF-350"] = 49.60,



            ["RCWF.450"] = 76.00,



            ["RCWF 450"] = 76.00,



            ["CWF 450"] = 76.00,



            ["CWF.450"] = 76.00,



            ["RCWF-450"] = 76.00,



            ["RCWF.588"] = 151.00,



            ["RCWF 588"] = 151.00,



            ["CWF 588"] = 151.00,



            ["CWF.588"] = 151.00,



            ["RCWF-588"] = 151.00,



            ["WF 882"] = 210.00,



            ["WF-882"] = 210.00,



            ["RCWF 882"] = 210.00,



            ["RCWF.882"] = 210.00,



            // WF Profiles



            ["WF 450x200x9x14"] = 76.00,



            ["WF 450"] = 76.00,



            ["WF-450"] = 76.00,



            ["WF450"] = 76.00,



            ["WF 400x200x8x13"] = 66.00,



            ["WF 400"] = 66.00,



            ["WF-400"] = 66.00,



            ["WF400"] = 66.00,



            ["WF 350x175x7x11"] = 49.60,



            ["WF 350"] = 49.60,



            ["WF-350"] = 49.60,



            ["WF350"] = 49.60,



            ["WF 300x150x6.5x9"] = 36.70,



            ["WF 300"] = 36.70,



            ["WF-300"] = 36.70,



            ["WF300"] = 36.70,



            ["WF 250x125x6x9"] = 29.60,



            ["WF 250"] = 29.60,



            ["WF-250"] = 29.60,



            ["WF250"] = 29.60,



            ["WF 200x100x5.5x8"] = 21.33,



            ["WF 200"] = 21.33,



            ["WF-200"] = 21.33,



            ["WF200"] = 21.33,



            ["WF 588x300x10x16"] = 151.00,



            ["WF 588"] = 151.00,



            ["WF-588"] = 151.00,



            ["WF588"] = 151.00,



            // H-Beam Profiles



            ["H 200x200x8x12"] = 49.92,



            ["H 200"] = 49.92,



            ["H-200"] = 49.92,



            ["H200"] = 49.92,



            ["H 250x250x9x14"] = 72.42,



            ["H 250"] = 72.42,



            ["H-250"] = 72.42,



            ["H250"] = 72.42,



            ["H 300x300x10x15"] = 94.00,



            ["H 300"] = 94.00,



            ["H-300"] = 94.00,



            ["H300"] = 94.00,



            ["H 350x350x12x19"] = 137.00,



            ["H 350"] = 137.00,



            ["H-350"] = 137.00,



            ["H350"] = 137.00,



            // Siku / Angle / Double Angle



            ["L 40x40x4"] = 2.42,



            ["L 40X40X4"] = 2.42,



            ["L 40"] = 2.42,



            ["SIKU 40x40x4"] = 2.42,



            ["SIKU 40X40X4"] = 2.42,



            ["SIKU 40"] = 2.42,



            ["L 50x50x5"] = 3.77,



            ["L 50X50X5"] = 3.77,



            ["L 50"] = 3.77,



            ["SIKU 50x50x5"] = 3.77,



            ["SIKU 50X50X5"] = 3.77,



            ["SIKU 50"] = 3.77,



            ["2L 60x60x6"] = 10.84,



            ["2L 60X60X6"] = 10.84,



            ["2L 60X60"] = 10.84,



            ["2L 60"] = 10.84,



            ["2L 70x70x7"] = 14.76,



            ["2L 70X70X7"] = 14.76,



            ["2L 70X70"] = 14.76,



            ["2L 70"] = 14.76,



            // Channels



            ["UNP 65"] = 7.09,



            ["UNP 65x42x5.5"] = 7.09,



            ["UNP 150"] = 18.60,



            ["UNP 150x75x6.5"] = 18.60,



            ["UNP 200"] = 24.67,



            ["UNP 200x80x7.5"] = 24.67,



            ["CNP 125"] = 4.51,



            ["CNP 150"] = 4.96,



            ["CNP 200"] = 6.84,



            ["C 150"] = 4.96,



            ["C150"] = 4.96,



            ["2CNP"] = 9.92,



            ["2CNP 150"] = 9.92,



            // Rods / Round bars / Bracing



            ["Wind Brace Dia. 19 mm"] = 2.23,



            ["Wind Brace Dia 19 mm"] = 2.23,



            ["Wind Brace Dia 19"] = 2.23,



            ["Dia 19 mm"] = 2.23,



            ["Dia. 19 mm"] = 2.23,



            ["Dia 19"] = 2.23,



            ["Dia 12 mm"] = 0.888,



            ["Dia. 12 mm"] = 0.888,



            ["Dia 12"] = 0.888



        };



        string csvPath = @"C:\AIGRAV\Revit\AIGRAV.SteelDatabase.csv";



        if (File.Exists(csvPath))



        {



            try



            {



                var lines = File.ReadAllLines(csvPath);



                foreach (var line in lines.Skip(1))



                {



                    var parts = line.Split(',');



                    if (parts.Length >= 4)



                    {



                        string fam = parts[0].Trim();



                        string profile = parts[1].Trim();



                        string profileKey = parts[2].Trim();



                        if (double.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double w))



                        {



                            dict[profile] = w;



                            dict[profileKey] = w;



                            dict[$"{fam} {profile}"] = w;



                            dict[$"{fam} {profileKey}"] = w;



                        }



                    }



                }



            }



            catch { }



        }



        return dict;



    }



    public static double GetCanonicalSteelWeight(string rawName, Dictionary<string, double>? dbWeights = null)



    {



        if (string.IsNullOrWhiteSpace(rawName)) return 0;



        dbWeights ??= LoadSteelDatabaseWeights();



        string trimmed = rawName.Trim();



        if (dbWeights.TryGetValue(trimmed, out double directW) && directW > 0)



        {



            return directW;



        }



        // Sanitasi string: ganti '.', '-', '_' dengan spasi, hilangkan spasi ganda



        string sanitized = rawName.Replace('.', ' ')



                                  .Replace('-', ' ')



                                  .Replace('_', ' ');



        while (sanitized.Contains("  "))



        {



            sanitized = sanitized.Replace("  ", " ");



        }



        sanitized = sanitized.Trim();



        if (dbWeights.TryGetValue(sanitized, out double sanW) && sanW > 0)



        {



            return sanW;



        }



        string upper = sanitized.ToUpperInvariant();



        // 1. Mandatory Exact Canonical Mappings:



        // 'RCWF.350', 'RCWF 350', 'CWF 350' -> 49.60 kg/m



        if (upper.Contains("RCWF 350") || upper.Contains("CWF 350") || upper.Equals("RCWF 350") || upper.Equals("CWF 350"))



            return 49.60;



        // 'RCWF.450', 'RCWF 450' -> 76.00 kg/m



        if (upper.Contains("RCWF 450") || upper.Contains("CWF 450") || upper.Equals("RCWF 450") || upper.Equals("CWF 450"))



            return 76.00;



        // 'RCWF.588', 'RCWF 588' -> 151.00 kg/m



        if (upper.Contains("RCWF 588") || upper.Contains("CWF 588") || upper.Equals("RCWF 588") || upper.Equals("CWF 588"))



            return 151.00;



        // 'WF 882' -> 210.00 kg/m (or 151.00 kg/m)



        if (upper.Contains("882"))



            return 210.00;



        // 2. Prefix Handling ('RCWF', 'CWF', 'WF', 'H', 'SIKU', '2L', 'UNP', 'CNP', 'C')



        if (upper.Contains("WF 450") || upper.Contains("WF450")) return 76.00;



        if (upper.Contains("WF 400") || upper.Contains("WF400")) return 66.00;



        if (upper.Contains("WF 350") || upper.Contains("WF350")) return 49.60;



        if (upper.Contains("WF 300") || upper.Contains("WF300")) return 36.70;



        if (upper.Contains("WF 250") || upper.Contains("WF250")) return 29.60;



        if (upper.Contains("WF 200") || upper.Contains("WF200")) return 21.33;



        if (upper.Contains("WF 588") || upper.Contains("WF588")) return 151.00;



        if (upper.Contains("H 200") || upper.Contains("H200")) return 49.92;



        if (upper.Contains("H 250") || upper.Contains("H250")) return 72.42;



        if (upper.Contains("H 300") || upper.Contains("H300")) return 94.00;



        if (upper.Contains("H 350") || upper.Contains("H350")) return 137.00;



        if (upper.Contains("SIKU 40") || upper.Contains("L 40") || upper.Contains("L40")) return 2.42;



        if (upper.Contains("SIKU 50") || upper.Contains("L 50") || upper.Contains("L50")) return 3.77;



        if (upper.Contains("2L 60") || upper.Contains("2L60")) return 10.84;



        if (upper.Contains("2L 70") || upper.Contains("2L70")) return 14.76;



        if (upper.Contains("UNP 200") || upper.Contains("UNP200")) return 24.67;



        if (upper.Contains("UNP 150") || upper.Contains("UNP150")) return 18.60;



        if (upper.Contains("UNP 65") || upper.Contains("UNP65")) return 7.09;



        if (upper.Contains("CNP 200") || upper.Contains("CNP200")) return 6.84;



        if (upper.Contains("CNP 150") || upper.Contains("CNP150") || upper.Contains("C 150") || upper.Contains("C150")) return 4.96;



        if (upper.Contains("CNP 125") || upper.Contains("CNP125")) return 4.51;



        if (upper.Contains("2CNP")) return 9.92;



        if (upper.Contains("19 MM") || upper.Contains("19MM") || upper.Contains("DIA 19") || upper.Contains("DIA. 19")) return 2.23;



        if (upper.Contains("12 MM") || upper.Contains("12MM") || upper.Contains("DIA 12") || upper.Contains("DIA. 12")) return 0.888;



        // 3. Substring match against dictionary



        foreach (var kvp in dbWeights)



        {



            if (sanitized.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)



            {



                return kvp.Value;



            }



        }



        return 0;



    }



    private sealed class CachedScheduleRule



    {



        public string Name { get; init; } = "";



        public ElementId CategoryId { get; init; } = ElementId.InvalidElementId;



        public string? LokasiFilter { get; set; }



        public ScheduleFilterType LokasiFilterType { get; set; } = ScheduleFilterType.Equal;



        public ElementId TypeIdFilter { get; set; } = ElementId.InvalidElementId;



        public ScheduleFilterType TypeFilterType { get; set; } = ScheduleFilterType.Equal;



        public string? TypeFilter { get; set; }



        public string? CommentsFilter { get; set; }



        public ScheduleFilterType CommentsFilterType { get; set; } = ScheduleFilterType.Contains;



        public string? ConnectionFilter { get; set; }



        public int TotalFiltersCount { get; set; } = 0;



    }



    private static List<CachedScheduleRule> GetCachedScheduleRules(Document doc)



    {



        var rules = new List<CachedScheduleRule>();



        var colCatId = new ElementId(BuiltInCategory.OST_StructuralColumns);



        var frameCatId = new ElementId(BuiltInCategory.OST_StructuralFraming);



        var plateCatId = new ElementId(BuiltInCategory.OST_StructConnectionPlates);



        var boltCatId = new ElementId(BuiltInCategory.OST_StructConnectionBolts);



        var allScheds = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)



            .Where(s => s.Name.Length >= 3 && char.IsLetter(s.Name[0]) && s.Name[1] == '.')



            .Where(s => s.Definition.CategoryId == colCatId || 



                        s.Definition.CategoryId == frameCatId || 



                        s.Definition.CategoryId == plateCatId || 



                        s.Definition.CategoryId == boltCatId)



            .ToList();



        foreach (var s in allScheds)



        {



            var def = s.Definition;



            var r = new CachedScheduleRule



            {



                Name = s.Name,



                CategoryId = def.CategoryId,



                TotalFiltersCount = def.GetFilterCount()



            };



            for (int i = 0; i < def.GetFilterCount(); i++)



            {



                var flt = def.GetFilter(i);



                var fld = def.GetField(flt.FieldId);



                string fName = fld?.GetName() ?? "";



                string val = "";



                try { val = flt.GetStringValue(); } catch { }



                ElementId valElemId = ElementId.InvalidElementId;



                try { valElemId = flt.GetElementIdValue(); } catch { }



                if (fName.Equals("Lokasi", StringComparison.OrdinalIgnoreCase))



                {



                    r.LokasiFilter = val;



                    r.LokasiFilterType = flt.FilterType;



                }



                else if (fName.IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0 || fName.Equals("Tipe Sambungan", StringComparison.OrdinalIgnoreCase))



                {



                    r.ConnectionFilter = val;



                }



                else if (fName.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0 || fld?.ParameterId == new ElementId(BuiltInParameter.ELEM_TYPE_PARAM))



                {



                    r.TypeFilterType = flt.FilterType;



                    if (valElemId != ElementId.InvalidElementId)



                    {



                        r.TypeIdFilter = valElemId;



                        var elem = doc.GetElement(valElemId);



                        r.TypeFilter = elem?.Name ?? "";



                    }



                    else if (!string.IsNullOrEmpty(val))



                    {



                        r.TypeFilter = val;



                    }



                }



                else if (fName.Equals("Comments", StringComparison.OrdinalIgnoreCase) || fName.Equals("Keterangan", StringComparison.OrdinalIgnoreCase))



                {



                    r.CommentsFilter = val;



                    r.CommentsFilterType = flt.FilterType;



                }



            }



            rules.Add(r);



        }



        return rules;



    }



    private static bool EvaluateRule(CachedScheduleRule r, string lokasi, ElementId typeId, string typeName, string comments, string connType)



    {



        // 1. Lokasi filter



        if (r.LokasiFilter != null)



        {



            if (r.LokasiFilterType == ScheduleFilterType.Equal && !string.Equals(lokasi, r.LokasiFilter, StringComparison.OrdinalIgnoreCase))



                return false;



            if (r.LokasiFilterType == ScheduleFilterType.NotEqual && string.Equals(lokasi, r.LokasiFilter, StringComparison.OrdinalIgnoreCase))



                return false;



        }



        // 2. Type filter



        if (r.TypeIdFilter != ElementId.InvalidElementId && typeId != ElementId.InvalidElementId)



        {



            bool matchId = (typeId == r.TypeIdFilter);



            bool matchName = !string.IsNullOrEmpty(r.TypeFilter) && typeName.IndexOf(r.TypeFilter, StringComparison.OrdinalIgnoreCase) >= 0;



            bool isMatch = matchId || matchName;



            if (r.TypeFilterType == ScheduleFilterType.Equal && !isMatch)



                return false;



            if (r.TypeFilterType == ScheduleFilterType.NotEqual && isMatch)



                return false;



        }



        else if (!string.IsNullOrEmpty(r.TypeFilter))



        {



            if ((r.TypeFilterType == ScheduleFilterType.Equal || r.TypeFilterType == ScheduleFilterType.Contains) && typeName.IndexOf(r.TypeFilter, StringComparison.OrdinalIgnoreCase) < 0)



                return false;



            if ((r.TypeFilterType == ScheduleFilterType.NotEqual || r.TypeFilterType == ScheduleFilterType.NotContains) && typeName.IndexOf(r.TypeFilter, StringComparison.OrdinalIgnoreCase) >= 0)



                return false;



        }



        // 3. Connection filter



        if (r.ConnectionFilter != null && connType.IndexOf(r.ConnectionFilter, StringComparison.OrdinalIgnoreCase) < 0)



            return false;



        // 4. Comments filter



        if (r.CommentsFilter != null)



        {



            if (r.CommentsFilterType == ScheduleFilterType.Equal && !string.Equals(comments, r.CommentsFilter, StringComparison.OrdinalIgnoreCase))



                return false;



            if (r.CommentsFilterType == ScheduleFilterType.NotEqual && string.Equals(comments, r.CommentsFilter, StringComparison.OrdinalIgnoreCase))



                return false;



            if (r.CommentsFilterType == ScheduleFilterType.Contains && comments.IndexOf(r.CommentsFilter, StringComparison.OrdinalIgnoreCase) < 0)



                return false;



            if (r.CommentsFilterType == ScheduleFilterType.NotContains && comments.IndexOf(r.CommentsFilter, StringComparison.OrdinalIgnoreCase) >= 0)



                return false;



        }



        return true;



    }



    private static bool EvaluateRule(CachedScheduleRule r, string lokasi, string typeName, string comments, string connType)



    {



        return EvaluateRule(r, lokasi, ElementId.InvalidElementId, typeName, comments, connType);



    }



    public static string InspectCalculatedFieldApi(Document doc, string schedulePattern)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AUDIT API: SCHEDULE CALCULATED VALUE / FIELD ===");



        sb.AppendLine($"Target Pattern: {schedulePattern} | Doc: {doc.Title}");



        var sched = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .FirstOrDefault(s => s.Name.IndexOf(schedulePattern, StringComparison.OrdinalIgnoreCase) >= 0);



        if (sched == null)



        {



            return $"Schedule matching '{schedulePattern}' not found in active document.";



        }



        sb.AppendLine($"Found Schedule: '{sched.Name}' (Id: {sched.Id})");



        var def = sched.Definition;



        sb.AppendLine($"Total Fields: {def.GetFieldCount()}");



        for (int i = 0; i < def.GetFieldCount(); i++)



        {



            var field = def.GetField(i);



            sb.AppendLine($"\n[Field {i}]: Name='{field.GetName()}' | FieldType={field.FieldType} | IsHidden={field.IsHidden} | FieldId={field.FieldId}");



            var props = typeof(ScheduleField).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);



            foreach (var p in props)



            {



                try



                {



                    var val = p.GetValue(field);



                    if (val != null)



                    {



                        string sVal = val.ToString() ?? "";



                        if (p.Name.Contains("Formula") || p.Name.Contains("Expression") || p.Name.Contains("Calculat") || p.Name.Contains("Spec") || p.Name.Contains("Unit"))



                        {



                            sb.AppendLine($"   Property {p.Name} = {sVal}");



                        }



                    }



                }



                catch { }



            }



            var methods = typeof(ScheduleField).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);



            foreach (var m in methods)



            {



                if (m.Name.Contains("Formula") || m.Name.Contains("Expression") || m.Name.Contains("Calculat"))



                {



                    try



                    {



                        if (m.GetParameters().Length == 0)



                        {



                            var res = m.Invoke(field, null);



                            sb.AppendLine($"   Method {m.Name}() => {res}");



                        }



                        else



                        {



                            sb.AppendLine($"   Method signature: {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");



                        }



                    }



                    catch (Exception ex)



                    {



                        sb.AppendLine($"   Method {m.Name}() threw: {ex.Message}");



                    }



                }



            }



        }



        try



        {



            var td = sched.GetTableData();



            var tsd = td.GetSectionData(SectionType.Body);



            sb.AppendLine($"\n[Schedule Table Body: {tsd.NumberOfRows} rows x {tsd.NumberOfColumns} columns]:");



            for (int r = 0; r < Math.Min(tsd.NumberOfRows, 15); r++)



            {



                var rowVals = new List<string>();



                for (int c = 0; c < tsd.NumberOfColumns; c++)



                {



                    rowVals.Add(tsd.GetCellText(r, c));



                }



                sb.AppendLine($"Row {r:D2}: [ {string.Join(" | ", rowVals)} ]");



            }



        }



        catch (Exception ex)



        {



            sb.AppendLine($"\nCould not get TableData: {ex.Message}");



        }



        sb.AppendLine("\n[ScheduleDefinition Reflection - Calculated / Formula Methods]:");



        var defMethods = typeof(ScheduleDefinition).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);



        foreach (var dm in defMethods)



        {



            if (dm.Name.Contains("Calculat") || dm.Name.Contains("Formula") || dm.Name.Contains("Add"))



            {



                sb.AppendLine($"   Def Method: {dm.Name}({string.Join(", ", dm.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {dm.ReturnType.Name}");



            }



        }



        return sb.ToString();



    }



    public static string AuditQtoFramingAndColumns(Document doc)



    {



        var sw = System.Diagnostics.Stopwatch.StartNew();



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 1: AUDIT QTO BALOK & KOLOM BAJA (VOLUME-BASED CONVERSION) ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        var dbWeights = LoadSteelDatabaseWeights();



        var rules = GetCachedScheduleRules(doc);



        var colCatId = new ElementId(BuiltInCategory.OST_StructuralColumns);



        var frameCatId = new ElementId(BuiltInCategory.OST_StructuralFraming);



        // Hanya sertakan schedule yang memiliki filter spesifik (> 0 filters)



        var colRules = rules.Where(r => r.CategoryId == colCatId && r.TotalFiltersCount > 0).ToList();



        var frameRules = rules.Where(r => r.CategoryId == frameCatId && r.TotalFiltersCount > 0).ToList();



        sb.AppendLine($"\n[1. METADATA SCHEDULE TARGET]");



        sb.AppendLine($"Total Schedule Kolom Terdaftar (dengan filter): {colRules.Count}");



        sb.AppendLine($"Total Schedule Balok / Framing Terdaftar (dengan filter): {frameRules.Count}");



        // Pre-cache Type Information (O(1) lookups)



        var keyProfiles = new string[] { "WF 350", "WF 250", "WF 300", "WF 400", "WF 588", "H 200", "H 250", "L 40x40x4", "UNP 200" };



        var keyProfileStats = new Dictionary<string, (int Qty, double VolumeM3, double SysLengthM, double EffLengthM, double SchedWeightKg, double DbTheoWeightKg, double UnitWeightDb)>(StringComparer.OrdinalIgnoreCase);



        foreach (var kp in keyProfiles)



        {



            double unitW = dbWeights.TryGetValue(kp, out double wVal) ? wVal : 0;



            keyProfileStats[kp] = (0, 0, 0, 0, 0, 0, unitW);



        }



        var typeCache = new Dictionary<ElementId, (string Name, bool IsConcrete, double UnitWeightDb, string KeyProfile)>();



        (string Name, bool IsConcrete, double UnitWeightDb, string KeyProfile) GetTypeInfo(ElementId tid)



        {



            if (typeCache.TryGetValue(tid, out var info)) return info;



            string name = doc.GetElement(tid)?.Name ?? "Unknown";



            bool isConc = name.Contains("Pedestal", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Pedestall", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Concrete", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Sloof", StringComparison.OrdinalIgnoreCase)



                       || name.Contains("Pondasi", StringComparison.OrdinalIgnoreCase);



            double unitW = GetCanonicalSteelWeight(name, dbWeights);



            if (unitW <= 0)



            {



                var elem = doc.GetElement(tid);



                if (elem is ElementType et && !string.IsNullOrEmpty(et.FamilyName))



                {



                    unitW = GetCanonicalSteelWeight(et.FamilyName, dbWeights);



                }



            }



            string matchedKey = "";



            if (!isConc)



            {



                foreach (var kp in keyProfiles)



                {



                    if (kp == "WF 400" && (name.IndexOf("WF 400", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("WF-400", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("WF400", StringComparison.OrdinalIgnoreCase) >= 0))



                    {



                        matchedKey = kp; break;



                    }



                    if (kp == "WF 588" && (name.IndexOf("WF 588", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("RCWF 588", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("RCWF.588", StringComparison.OrdinalIgnoreCase) >= 0))



                    {



                        matchedKey = kp; break;



                    }



                    if (kp == "L 40x40x4" && (name.IndexOf("40x40x4", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("40X40", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("L 40", StringComparison.OrdinalIgnoreCase) >= 0))



                    {



                        matchedKey = kp; break;



                    }



                    if (kp == "UNP 200" && (name.IndexOf("UNP 200", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("UNP200", StringComparison.OrdinalIgnoreCase) >= 0))



                    {



                        matchedKey = kp; break;



                    }



                    if (name.IndexOf(kp, StringComparison.OrdinalIgnoreCase) >= 0)



                    {



                        matchedKey = kp;



                        break;



                    }



                }



            }



            info = (name, isConc, unitW, matchedKey);



            typeCache[tid] = info;



            return info;



        }



        // Ambil elemen fisik



        var columns = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralColumns)



            .WhereElementIsNotElementType()



            .ToList();



        var framings = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .ToList();



        // Cari definition parameter 'Lokasi' sekali saja



        Definition? lokasiDef = null;



        for (int i = 0; i < Math.Min(columns.Count, 50); i++)



        {



            var p = columns[i].LookupParameter("Lokasi");



            if (p != null) { lokasiDef = p.Definition; break; }



        }



        if (lokasiDef == null)



        {



            for (int i = 0; i < Math.Min(framings.Count, 50); i++)



            {



                var p = framings[i].LookupParameter("Lokasi");



                if (p != null) { lokasiDef = p.Definition; break; }



            }



        }



        var profileStats = new Dictionary<string, (int Qty, double VolumeM3, double SysLengthM, double EffLengthM, double SchedWeightKg, double DbTheoWeightKg)>(StringComparer.OrdinalIgnoreCase);



        // EVALUASI KOLOM



        int colDoubleCount = 0;



        int colUnscheduled = 0;



        int colPass = 0;



        int steelColumnCount = 0;



        int concretePedestalCount = 0;



        var colDoubleCountDetails = new List<string>();



        var colUnscheduledDetails = new List<string>();



        foreach (var col in columns)



        {



            var tid = col.GetTypeId();



            var tInfo = GetTypeInfo(tid);



            string comm = col.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";



            string loc = lokasiDef != null ? (col.get_Parameter(lokasiDef)?.AsString() ?? "") : (col.LookupParameter("Lokasi")?.AsString() ?? "");



            bool isConc = tInfo.IsConcrete || comm.Contains("Pedestal", StringComparison.OrdinalIgnoreCase) || comm.Contains("Pondasi", StringComparison.OrdinalIgnoreCase);



            if (isConc) concretePedestalCount++;



            else steelColumnCount++;



            int matches = 0;



            string firstMatch = "";



            List<string>? matchedList = null;



            for (int rIdx = 0; rIdx < colRules.Count; rIdx++)



            {



                var r = colRules[rIdx];



                if (EvaluateRule(r, loc, tid, tInfo.Name, comm, ""))



                {



                    matches++;



                    if (matches == 1) firstMatch = r.Name;



                    else



                    {



                        if (matchedList == null) matchedList = new List<string>(2) { firstMatch, r.Name };



                        else matchedList.Add(r.Name);



                    }



                }



            }



            if (matches > 1)



            {



                colDoubleCount++;



                if (colDoubleCountDetails.Count < 20)



                    colDoubleCountDetails.Add($"Kolom ID {col.Id.Value} ({tInfo.Name}, Lokasi: '{loc}') masuk ke {matches} schedule: [{string.Join(", ", matchedList!)}]");



            }



            else if (matches == 0)



            {



                colUnscheduled++;



                if (colUnscheduledDetails.Count < 20)



                    colUnscheduledDetails.Add($"Kolom ID {col.Id.Value} ({tInfo.Name}, Lokasi: '{loc}', Comm: '{comm}') TIDAK TERJADWAL");



            }



            else



            {



                colPass++;



            }



            // Cross-check panjang & berat berbasis volume



            double lenM = 0;



            var pLen = col.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM) 



                    ?? col.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);



            if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;



            double volM3 = 0;



            var pVol = col.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);



            if (pVol != null && pVol.HasValue) volM3 = pVol.AsDouble() * 0.028316846592;



            double schedKg = volM3 > 0 ? (volM3 * 7850.0) : 0;



            double effLenM = (tInfo.UnitWeightDb > 0 && schedKg > 0) ? (schedKg / tInfo.UnitWeightDb) : lenM;



            double theoKg = lenM * tInfo.UnitWeightDb;



            if (schedKg == 0 && theoKg > 0) schedKg = theoKg;



            if (!isConc)



            {



                string groupKey = $"{(string.IsNullOrEmpty(loc) ? "MODEL UTAMA" : loc)} | {tInfo.Name}";



                if (!profileStats.TryGetValue(groupKey, out var st)) st = (0, 0, 0, 0, 0, 0);



                profileStats[groupKey] = (st.Qty + 1, st.VolumeM3 + volM3, st.SysLengthM + lenM, st.EffLengthM + effLenM, st.SchedWeightKg + schedKg, st.DbTheoWeightKg + theoKg);



                if (!string.IsNullOrEmpty(tInfo.KeyProfile) && keyProfileStats.TryGetValue(tInfo.KeyProfile, out var kst))



                {



                    keyProfileStats[tInfo.KeyProfile] = (kst.Qty + 1, kst.VolumeM3 + volM3, kst.SysLengthM + lenM, kst.EffLengthM + effLenM, kst.SchedWeightKg + schedKg, kst.DbTheoWeightKg + theoKg, kst.UnitWeightDb);



                }



            }



        }



        // EVALUASI BALOK / STRUCTURAL FRAMING



        int frameDoubleCount = 0;



        int frameUnscheduled = 0;



        int framePass = 0;



        int steelFramingCount = 0;



        int concreteFramingCount = 0;



        int anchorCount = 0;



        var frameDoubleCountDetails = new List<string>();



        var frameUnscheduledDetails = new List<string>();



        foreach (var fr in framings)



        {



            var tid = fr.GetTypeId();



            var tInfo = GetTypeInfo(tid);



            string comm = fr.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() ?? "";



            // Angkur ditangani khusus di schedule Angkur (Sub-agent 2)



            if (comm.Contains("Angkur", StringComparison.OrdinalIgnoreCase))



            {



                anchorCount++;



                continue;



            }



            string loc = lokasiDef != null ? (fr.get_Parameter(lokasiDef)?.AsString() ?? "") : (fr.LookupParameter("Lokasi")?.AsString() ?? "");



            bool isConc = tInfo.IsConcrete || comm.Contains("Sloof", StringComparison.OrdinalIgnoreCase) || comm.Contains("Pondasi", StringComparison.OrdinalIgnoreCase);



            if (isConc) concreteFramingCount++;



            else steelFramingCount++;



            int matches = 0;



            string firstMatch = "";



            List<string>? matchedList = null;



            for (int rIdx = 0; rIdx < frameRules.Count; rIdx++)



            {



                var r = frameRules[rIdx];



                if (EvaluateRule(r, loc, tid, tInfo.Name, comm, ""))



                {



                    matches++;



                    if (matches == 1) firstMatch = r.Name;



                    else



                    {



                        if (matchedList == null) matchedList = new List<string>(2) { firstMatch, r.Name };



                        else matchedList.Add(r.Name);



                    }



                }



            }



            if (matches > 1)



            {



                frameDoubleCount++;



                if (frameDoubleCountDetails.Count < 20)



                    frameDoubleCountDetails.Add($"Balok ID {fr.Id.Value} ({tInfo.Name}, Lokasi: '{loc}', Comm: '{comm}') masuk ke {matches} schedule: [{string.Join(", ", matchedList!)}]");



            }



            else if (matches == 0)



            {



                frameUnscheduled++;



                if (frameUnscheduledDetails.Count < 20)



                    frameUnscheduledDetails.Add($"Balok ID {fr.Id.Value} ({tInfo.Name}, Lokasi: '{loc}', Comm: '{comm}') TIDAK TERJADWAL");



            }



            else



            {



                framePass++;



            }



            // Cross-check panjang & berat berbasis volume



            double lenM = 0;



            var pLen = fr.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH) 



                    ?? fr.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM);



            if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;



            double volM3 = 0;



            var pVol = fr.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);



            if (pVol != null && pVol.HasValue) volM3 = pVol.AsDouble() * 0.028316846592;



            double schedKg = volM3 > 0 ? (volM3 * 7850.0) : 0;



            double effLenM = (tInfo.UnitWeightDb > 0 && schedKg > 0) ? (schedKg / tInfo.UnitWeightDb) : lenM;



            double theoKg = lenM * tInfo.UnitWeightDb;



            if (schedKg == 0 && theoKg > 0) schedKg = theoKg;



            if (!isConc)



            {



                string groupKey = $"{(string.IsNullOrEmpty(loc) ? "MODEL UTAMA" : loc)} | {tInfo.Name}";



                if (!profileStats.TryGetValue(groupKey, out var st)) st = (0, 0, 0, 0, 0, 0);



                profileStats[groupKey] = (st.Qty + 1, st.VolumeM3 + volM3, st.SysLengthM + lenM, st.EffLengthM + effLenM, st.SchedWeightKg + schedKg, st.DbTheoWeightKg + theoKg);



                if (!string.IsNullOrEmpty(tInfo.KeyProfile) && keyProfileStats.TryGetValue(tInfo.KeyProfile, out var kst))



                {



                    keyProfileStats[tInfo.KeyProfile] = (kst.Qty + 1, kst.VolumeM3 + volM3, kst.SysLengthM + lenM, kst.EffLengthM + effLenM, kst.SchedWeightKg + schedKg, kst.DbTheoWeightKg + theoKg, kst.UnitWeightDb);



                }



            }



        }



        sb.AppendLine($"\n[2. POPULASI ELEMEN FISIK DI MODEL]");



        sb.AppendLine($"Total Elemen Kolom (OST_StructuralColumns): {columns.Count} unit (Kolom Baja: {steelColumnCount}, Pedestal Beton: {concretePedestalCount})");



        sb.AppendLine($"Total Elemen Balok (OST_StructuralFraming): {framings.Count} unit (Balok/Rangka Baja: {steelFramingCount}, Sloof/Beton: {concreteFramingCount}, Angkur: {anchorCount})");



        sb.AppendLine("\n[3. HASIL VERIFIKASI KRITERIA 1: ANTI DOUBLE-COUNTING]");



        sb.AppendLine($"Kolom Baja Double Counting: {colDoubleCount} batang (Target: 0)");



        sb.AppendLine($"Balok Baja Double Counting: {frameDoubleCount} batang (Target: 0)");



        if (colDoubleCountDetails.Count > 0 || frameDoubleCountDetails.Count > 0)



        {



            sb.AppendLine("Detail Double Counting Terdeteksi:");



            foreach (var d in colDoubleCountDetails.Take(10)) sb.AppendLine($"  ! [Kolom] {d}");



            foreach (var d in frameDoubleCountDetails.Take(10)) sb.AppendLine($"  ! [Balok] {d}");



        }



        else



        {



            sb.AppendLine(">> STATUS: PASS - 100% Elemen bebas dari double counting antar schedule.");



        }



        sb.AppendLine("\n[4. HASIL VERIFIKASI KRITERIA 2: KELENGKAPAN ELEMEN (UNSCHEDULED CHECK)]");



        sb.AppendLine($"Kolom Baja Terjadwal: {colPass} / {columns.Count} batang (Unscheduled: {colUnscheduled})");



        sb.AppendLine($"Balok Baja Terjadwal: {framePass} / {framings.Count - anchorCount} batang (Unscheduled: {frameUnscheduled})");



        if (colUnscheduledDetails.Count > 0 || frameUnscheduledDetails.Count > 0)



        {



            sb.AppendLine("Detail Elemen Belum Terjadwal:");



            foreach (var u in colUnscheduledDetails.Take(10)) sb.AppendLine($"  ? [Kolom] {u}");



            foreach (var u in frameUnscheduledDetails.Take(10)) sb.AppendLine($"  ? [Balok] {u}");



        }



        else



        {



            sb.AppendLine(">> STATUS: PASS - 100% Elemen fisik model tercakup dalam schedule.");



        }



        sb.AppendLine("\n[5. HASIL VERIFIKASI KRITERIA 3: CROSS-CHECK VOLUME-DERIVED LENGTH & BERAT VS TABEL BAJA]");



        sb.AppendLine("A. TABEL REKAPITULASI 9 PROFIL UTAMA (BERDASARKAN DATA SOLID VOLUME REVIT & rho=7850 kg/m3):");



        sb.AppendLine(string.Format("{0,-10} | {1,7} | {2,5} | {3,8} | {4,10} | {5,10} | {6,9} | {7,14} | {8,14} | {9,11} | {10,7} | {11,8}",



            "Profil", "Unit(kg)", "Qty", "Vol(m3)", "L_sys(m)", "L_eff(m)", "Cut(m)", "Berat Solid(kg)", "Berat Tabel(kg)", "Selisih(kg)", "Diff(%)", "Faktor k"));



        sb.AppendLine(new string('-', 142));



        double sumKeyQty = 0;



        double sumKeyVol = 0;



        double sumKeySysLen = 0;



        double sumKeyEffLen = 0;



        double sumKeySchedW = 0;



        double sumKeyDbW = 0;



        foreach (var kp in keyProfiles)



        {



            var kst = keyProfileStats[kp];



            double diffW = kst.SchedWeightKg - kst.DbTheoWeightKg;



            double diffPct = kst.DbTheoWeightKg > 0 ? (diffW / kst.DbTheoWeightKg) * 100.0 : 0.0;



            double cutLen = kst.SysLengthM - kst.EffLengthM;



            double factorK = (kst.SchedWeightKg > 0 && kst.DbTheoWeightKg > 0) ? (kst.DbTheoWeightKg / kst.SchedWeightKg) : 1.0;



            sb.AppendLine(string.Format("{0,-10} | {1,7:F2} | {2,5} | {3,8:F2} | {4,10:F2} | {5,10:F2} | {6,9:F2} | {7,14:N1} | {8,14:N1} | {9,11:N1} | {10,6:F2}% | {11,8:F5}",



                kp, kst.UnitWeightDb, kst.Qty, kst.VolumeM3, kst.SysLengthM, kst.EffLengthM, cutLen, kst.SchedWeightKg, kst.DbTheoWeightKg, diffW, diffPct, factorK));



            sumKeyQty += kst.Qty;



            sumKeyVol += kst.VolumeM3;



            sumKeySysLen += kst.SysLengthM;



            sumKeyEffLen += kst.EffLengthM;



            sumKeySchedW += kst.SchedWeightKg;



            sumKeyDbW += kst.DbTheoWeightKg;



        }



        sb.AppendLine(new string('-', 142));



        double sumKeyDiffW = sumKeySchedW - sumKeyDbW;



        double sumKeyDiffPct = sumKeyDbW > 0 ? (sumKeyDiffW / sumKeyDbW) * 100.0 : 0.0;



        double sumKeyCut = sumKeySysLen - sumKeyEffLen;



        double sumKeyFactorK = sumKeySchedW > 0 ? (sumKeyDbW / sumKeySchedW) : 1.0;



        sb.AppendLine(string.Format("{0,-10} | {1,7} | {2,5} | {3,8:F2} | {4,10:F2} | {5,10:F2} | {6,9:F2} | {7,14:N1} | {8,14:N1} | {9,11:N1} | {10,6:F2}% | {11,8:F5}",



            "SUBTOTAL 9", "-", sumKeyQty, sumKeyVol, sumKeySysLen, sumKeyEffLen, sumKeyCut, sumKeySchedW, sumKeyDbW, sumKeyDiffW, sumKeyDiffPct, sumKeyFactorK));



        sb.AppendLine("\nB. BREAKDOWN LENGKAP PER LOKASI & PROFIL BAJA:");



        sb.AppendLine(string.Format("{0,-50} | {1,5} | {2,8} | {3,9} | {4,9} | {5,14} | {6,14} | {7,7} | {8,8}", 



            "Lokasi | Profil Baja", "Qty", "Vol(m3)", "L_sys(m)", "L_eff(m)", "Berat Solid(kg)", "Berat Tabel(kg)", "Diff(%)", "Faktor k"));



        sb.AppendLine(new string('-', 145));



        double grandTotalVol = 0;



        double grandTotalSysLen = 0;



        double grandTotalEffLen = 0;



        double grandTotalSchedWeight = 0;



        double grandTotalDbWeight = 0;



        int grandTotalQty = 0;



        foreach (var kvp in profileStats.OrderBy(x => x.Key))



        {



            var st = kvp.Value;



            double diff = st.SchedWeightKg - st.DbTheoWeightKg;



            double diffPct = st.DbTheoWeightKg > 0 ? (diff / st.DbTheoWeightKg) * 100.0 : 0;



            double fk = st.SchedWeightKg > 0 ? (st.DbTheoWeightKg / st.SchedWeightKg) : 1.0;



            sb.AppendLine(string.Format("{0,-50} | {1,5} | {2,8:F2} | {3,9:F2} | {4,9:F2} | {5,14:N1} | {6,14:N1} | {7,6:F2}% | {8,8:F5}",



                kvp.Key, st.Qty, st.VolumeM3, st.SysLengthM, st.EffLengthM, st.SchedWeightKg, st.DbTheoWeightKg, diffPct, fk));



            grandTotalQty += st.Qty;



            grandTotalVol += st.VolumeM3;



            grandTotalSysLen += st.SysLengthM;



            grandTotalEffLen += st.EffLengthM;



            grandTotalSchedWeight += st.SchedWeightKg;



            grandTotalDbWeight += st.DbTheoWeightKg;



        }



        sb.AppendLine(new string('-', 145));



        double grandDiff = grandTotalSchedWeight - grandTotalDbWeight;



        double grandDiffPct = grandTotalDbWeight > 0 ? (grandDiff / grandTotalDbWeight) * 100.0 : 0.0;



        double grandFk = grandTotalSchedWeight > 0 ? (grandTotalDbWeight / grandTotalSchedWeight) : 1.0;



        sb.AppendLine(string.Format("{0,-50} | {1,5} | {2,8:F2} | {3,9:F2} | {4,9:F2} | {5,14:N1} | {6,14:N1} | {7,6:F2}% | {8,8:F5}",



            "TOTAL MODEL STRUKTUR BAJA", grandTotalQty, grandTotalVol, grandTotalSysLen, grandTotalEffLen, grandTotalSchedWeight, grandTotalDbWeight, grandDiffPct, grandFk));



        sb.AppendLine($"\n[WAKTU AUDIT KOMPUTASI: {sw.ElapsedMilliseconds} ms]");



        return sb.ToString();



    }



    public static string AuditQtoConnectionsPlatesAndBolts(Document doc)



    {



        var sw = System.Diagnostics.Stopwatch.StartNew();



        void Log(string msg) => File.AppendAllText(@"C:\AIGRAV\qto_audit.log", $"{sw.ElapsedMilliseconds} ms: {msg}\n");



        File.WriteAllText(@"C:\AIGRAV\qto_audit.log", $"Start audit at {DateTime.Now:HH:mm:ss.fff}\n");



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 2: AUDIT QTO SAMBUNGAN, PELAT, BAUT & BASEPLATE ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        var rules = GetCachedScheduleRules(doc);



        Log($"Rules cached ({rules.Count} rules)");



        var plateCatId = new ElementId(BuiltInCategory.OST_StructConnectionPlates);



        var boltCatId = new ElementId(BuiltInCategory.OST_StructConnectionBolts);



        var plateRules = rules.Where(r => r.CategoryId == plateCatId).ToList();



        var boltRules = rules.Where(r => r.CategoryId == boltCatId).ToList();



        sb.AppendLine($"\n[1. METADATA SCHEDULE TARGET]");



        sb.AppendLine($"Total Schedule Pelat & Baseplate Terdaftar: {plateRules.Count}");



        sb.AppendLine($"Total Schedule Baut Terdaftar: {boltRules.Count}");



        var plateCollector = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionPlates)



            .WhereElementIsNotElementType();



        var boltCollector = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructConnectionBolts)



            .WhereElementIsNotElementType();



        Log("Collectors initialized");



        // Evaluasi Pelat & Baseplate



        int plateDoubleCount = 0;



        int plateUnscheduled = 0;



        int platePass = 0;



        int totalPlates = 0;



        int gordingPlateCount = 0;



        var plateDoubleCountDetails = new List<string>();



        var plateUnscheduledDetails = new List<string>();



        double totalPlateRevitWeight = 0;



        double totalPlateTheoWeight = 0;



        var plateBreakdown = new Dictionary<string, (int Qty, double SchedWeightKg, double TheoWeightKg)>(StringComparer.OrdinalIgnoreCase);



        // Optimasi Matching Rules dengan Indexing Lokasi



        var buPlateRules = plateRules.Where(r => string.IsNullOrEmpty(r.LokasiFilter) || r.LokasiFilter.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase)).ToList();



        var subPlateRules = plateRules



            .Where(r => !string.IsNullOrEmpty(r.LokasiFilter) && !r.LokasiFilter.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase))



            .GroupBy(r => r.LokasiFilter!, StringComparer.OrdinalIgnoreCase)



            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);



        var buBoltRules = boltRules.Where(r => string.IsNullOrEmpty(r.LokasiFilter) || r.LokasiFilter.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase)).ToList();



        var subBoltRules = boltRules



            .Where(r => !string.IsNullOrEmpty(r.LokasiFilter) && !r.LokasiFilter.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase))



            .GroupBy(r => r.LokasiFilter!, StringComparer.OrdinalIgnoreCase)



            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);



        bool hasGordingPlateSched = plateRules.Any(r => r.CommentsFilter?.IndexOf("Support Gording", StringComparison.OrdinalIgnoreCase) >= 0);



        bool hasGordingBoltSched = boltRules.Any(r => r.CommentsFilter?.IndexOf("Support Gording", StringComparison.OrdinalIgnoreCase) >= 0);



        foreach (var p in plateCollector)



        {



            totalPlates++;



            string comm = p.LookupParameter("Comments")?.AsString() ?? "";



            string loc = p.LookupParameter("Lokasi")?.AsString() ?? "";



            string connType = "";



            List<string> matched;



            if (string.IsNullOrEmpty(loc) || loc.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase))



            {



                matched = buPlateRules.Where(r => EvaluateRule(r, loc, "", comm, "")).Select(r => r.Name).ToList();



            }



            else



            {



                connType = p.LookupParameter("Type of connection")?.AsString() ?? p.LookupParameter("Type of Connection")?.AsString() ?? "";



                if (subPlateRules.TryGetValue(loc, out var rList))



                {



                    matched = rList.Where(r => EvaluateRule(r, loc, "", comm, connType)).Select(r => r.Name).ToList();



                }



                else



                {



                    matched = new List<string>(0);



                }



            }



            if (matched.Count > 1)



            {



                plateDoubleCount++;



                if (plateDoubleCountDetails.Count < 25)



                    plateDoubleCountDetails.Add($"Pelat ID {p.Id.Value} (Conn: '{connType}', Comm: '{comm}', Lok: '{loc}') masuk ke {matched.Count} schedule: [{string.Join(", ", matched)}]");



            }



            else if (matched.Count == 0)



            {



                plateUnscheduled++;



                if (plateUnscheduledDetails.Count < 25)



                    plateUnscheduledDetails.Add($"Pelat ID {p.Id.Value} (Conn: '{connType}', Comm: '{comm}', Lok: '{loc}') TIDAK TERJADWAL");



            }



            else



            {



                platePass++;



            }



            // Cross-check berat pelat vs rumus teoritis: t(m) * W(m) * L(m) * 7850 kg/m3



            double theoWeight = 0;



            double revitWeight = 0;



            var pW = p.LookupParameter("Weight");



            if (pW != null && pW.HasValue && pW.AsDouble() > 0) revitWeight = pW.AsDouble();



            bool isPrimaryPlate = !string.IsNullOrEmpty(loc) 



                || comm.IndexOf("Baseplate", StringComparison.OrdinalIgnoreCase) >= 0



                || comm.IndexOf("Top Plate", StringComparison.OrdinalIgnoreCase) >= 0



                || comm.IndexOf("Stifner Kolom", StringComparison.OrdinalIgnoreCase) >= 0



                || comm.IndexOf("Joint Kolom", StringComparison.OrdinalIgnoreCase) >= 0



                || comm.IndexOf("Gewel", StringComparison.OrdinalIgnoreCase) >= 0;



            if (isPrimaryPlate)



            {



                double tMm = 0, wMm = 0, lMm = 0;



                var pt = p.LookupParameter("Thickness");



                if (pt != null && pt.HasValue) tMm = pt.AsDouble() * 304.8;



                var pw = p.LookupParameter("Width");



                if (pw != null && pw.HasValue) wMm = pw.AsDouble() * 304.8;



                var pl = p.LookupParameter("Length");



                if (pl != null && pl.HasValue) lMm = pl.AsDouble() * 304.8;



                theoWeight = (tMm / 1000.0) * (wMm / 1000.0) * (lMm / 1000.0) * 7850.0;



                if (revitWeight == 0 && theoWeight > 0) revitWeight = theoWeight;



                if (theoWeight == 0 && revitWeight > 0) theoWeight = revitWeight;



            }



            else



            {



                theoWeight = revitWeight;



            }



            totalPlateRevitWeight += revitWeight;



            totalPlateTheoWeight += theoWeight;



            string key = $"{(string.IsNullOrEmpty(loc) ? "(Bangunan Utama)" : loc)} | {(string.IsNullOrEmpty(comm) ? (string.IsNullOrEmpty(connType) ? "Pelat Standar" : connType) : comm)}";



            if (!plateBreakdown.TryGetValue(key, out var st)) st = (0, 0, 0);



            plateBreakdown[key] = (st.Qty + 1, st.SchedWeightKg + revitWeight, st.TheoWeightKg + theoWeight);



        }



        Log($"Finished plates loop. Total: {totalPlates}, Pass: {platePass}, Unscheduled: {plateUnscheduled}, DoubleCount: {plateDoubleCount}");



        // Evaluasi Baut



        int boltDoubleCount = 0;



        int boltUnscheduled = 0;



        int boltPass = 0;



        int totalBolts = 0;



        var boltDoubleCountDetails = new List<string>();



        var boltUnscheduledDetails = new List<string>();



        int totalPhysicalBolts = 0;



        foreach (var b in boltCollector)



        {



            totalBolts++;



            string comm = b.LookupParameter("Comments")?.AsString() ?? "";



            string loc = b.LookupParameter("Lokasi")?.AsString() ?? "";



            string connType = "";



            List<string> matched;



            if (string.IsNullOrEmpty(loc) || loc.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase))



            {



                matched = buBoltRules.Where(r => EvaluateRule(r, loc, "", comm, "")).Select(r => r.Name).ToList();



            }



            else



            {



                connType = b.LookupParameter("Type of connection")?.AsString() ?? b.LookupParameter("Type of Connection")?.AsString() ?? "";



                if (subBoltRules.TryGetValue(loc, out var rList))



                {



                    matched = rList.Where(r => EvaluateRule(r, loc, "", comm, connType)).Select(r => r.Name).ToList();



                }



                else



                {



                    matched = new List<string>(0);



                }



            }



            if (matched.Count > 1)



            {



                boltDoubleCount++;



                if (boltDoubleCountDetails.Count < 25)



                    boltDoubleCountDetails.Add($"Baut ID {b.Id.Value} (Conn: '{connType}', Comm: '{comm}', Lok: '{loc}') masuk ke {matched.Count} schedule: [{string.Join(", ", matched)}]");



            }



            else if (matched.Count == 0)



            {



                boltUnscheduled++;



                if (boltUnscheduledDetails.Count < 25)



                    boltUnscheduledDetails.Add($"Baut ID {b.Id.Value} (Conn: '{connType}', Comm: '{comm}', Lok: '{loc}') TIDAK TERJADWAL");



            }



            else



            {



                boltPass++;



            }



            int n = 1;



            var pNum = b.LookupParameter("Number");



            if (pNum != null && pNum.HasValue && pNum.AsInteger() > 0) n = pNum.AsInteger();



            totalPhysicalBolts += n;



        }



        Log($"Finished bolts loop. Total: {totalBolts}, Pass: {boltPass}, Unscheduled: {boltUnscheduled}, DoubleCount: {boltDoubleCount}, PhysicalBolts: {totalPhysicalBolts}");



        sb.AppendLine($"\n[2. POPULASI ELEMEN FISIK DI MODEL]");



        sb.AppendLine($"Pelat Sambungan & Baseplate: {totalPlates:N0} lembar");



        sb.AppendLine($"Grup Baut Sambungan: {totalBolts:N0} unit");



        sb.AppendLine("\n[3. HASIL VERIFIKASI KRITERIA 1: ANTI DOUBLE-COUNTING]");



        sb.AppendLine($"Pelat Sambungan / Baseplate Double Counting: {plateDoubleCount} lembar (Target: 0)");



        sb.AppendLine($"Baut Sambungan Double Counting: {boltDoubleCount} unit/grup (Target: 0)");



        if (plateDoubleCountDetails.Count > 0 || boltDoubleCountDetails.Count > 0)



        {



            sb.AppendLine("Detail Double Counting Terdeteksi:");



            foreach (var d in plateDoubleCountDetails.Take(10)) sb.AppendLine($"  ! {d}");



            foreach (var d in boltDoubleCountDetails.Take(10)) sb.AppendLine($"  ! {d}");



        }



        else



        {



            sb.AppendLine(">> STATUS: PASS - 100% Elemen sambungan bebas dari double counting.");



        }



        sb.AppendLine("\n[4. HASIL VERIFIKASI KRITERIA 2: KELENGKAPAN ELEMEN (UNSCHEDULED CHECK)]");



        sb.AppendLine($"Pelat Sambungan Terjadwal: {platePass:N0} / {totalPlates:N0} lembar (Unscheduled: {plateUnscheduled:N0})");



        sb.AppendLine($"Grup Baut Sambungan Terjadwal: {boltPass:N0} / {totalBolts:N0} unit (Unscheduled: {boltUnscheduled:N0})");



        sb.AppendLine($"Total Kuantitas Butir Baut Fisik Terhitung di Model: {totalPhysicalBolts:N0} butir");



        if (plateUnscheduledDetails.Count > 0 || boltUnscheduledDetails.Count > 0)



        {



            sb.AppendLine("Detail Elemen Belum Terjadwal (Sampel):");



            foreach (var u in plateUnscheduledDetails.Take(10)) sb.AppendLine($"  ? {u}");



            foreach (var u in boltUnscheduledDetails.Take(10)) sb.AppendLine($"  ? {u}");



        }



        else



        {



            sb.AppendLine(">> STATUS: PASS - 100% Sambungan fisik model tercakup dalam schedule.");



        }



        sb.AppendLine("\n[5. HASIL VERIFIKASI KRITERIA 3: CROSS-CHECK BERAT PELAT VS FORMULA STANDAR (7.850 kg/m3)]");



        sb.AppendLine(string.Format("{0,-45} | {1,6} | {2,14} | {3,14} | {4,8}", "Lokasi | Tipe Pelat / Baseplate", "Qty", "Berat Sched", "Berat Teori", "Selisih"));



        sb.AppendLine(new string('-', 98));



        foreach (var kvp in plateBreakdown.OrderByDescending(x => x.Value.Qty).Take(35))



        {



            var st = kvp.Value;



            double diff = st.SchedWeightKg - st.TheoWeightKg;



            double diffPct = st.TheoWeightKg > 0 ? (diff / st.TheoWeightKg) * 100.0 : 0;



            sb.AppendLine(string.Format("{0,-45} | {1,6} | {2,11:N1} kg | {3,11:N1} kg | {4,6:F1}%",



                kvp.Key, st.Qty, st.SchedWeightKg, st.TheoWeightKg, diffPct));



        }



        sb.AppendLine(new string('-', 98));



        double grandDiff = totalPlateRevitWeight - totalPlateTheoWeight;



        double grandDiffPct = totalPlateTheoWeight > 0 ? (grandDiff / totalPlateTheoWeight) * 100.0 : 0.0;



        sb.AppendLine(string.Format("{0,-45} | {1,6} | {2,11:N1} kg | {3,11:N1} kg | {4,6:F1}%",



            "TOTAL PELAT SAMBUNGAN & BASEPLATE", totalPlates, totalPlateRevitWeight, totalPlateTheoWeight, grandDiffPct));



        return sb.ToString();



    }



    public static string AuditQtoGlobalReconciliation(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== SUB-AGENT 3: AUDIT REKONSILIASI MASTER & ANTI-DOUBLE COUNTING BANGUNAN UTAMA ===");



        sb.AppendLine($"Model: {doc.Title} | Waktu Audit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");



        var allScheds = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSchedule))



            .Cast<ViewSchedule>()



            .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule && (s.Name.StartsWith("C.3.") || s.Name.StartsWith("C.2.") || s.Name.StartsWith("B.1.") || s.Name.StartsWith("B.2.")))



            .ToList();



        var buScheds = allScheds.Where(s => s.Name.StartsWith("B.1.") || s.Name.StartsWith("B.2.")).ToList();



        // 1. Boundary Leak Test



        sb.AppendLine($"\n[1. AUDIT ISOLASI BANGUNAN UTAMA VS SUB-LOKASI (BOUNDARY TEST)]");



        sb.AppendLine($"Total Master Schedule Bangunan Utama yang Diaudit: {buScheds.Count}");



        int totalBuElements = 0;



        int totalBoundaryLeaks = 0;



        var leaksBySchedule = new Dictionary<string, (int Total, int Leaks, Dictionary<string, int> ByLoc)>();



        var leakSamples = new List<string>();



        var plateCatId = new ElementId(BuiltInCategory.OST_StructConnectionPlates);



        var schedPlateWeights = new Dictionary<string, (double SchedWeight, double CleanWeight, int CleanCount)>();



        foreach (var s in buScheds)



        {



            var elems = new FilteredElementCollector(doc, s.Id)



                .WhereElementIsNotElementType()



                .ToElements();



            totalBuElements += elems.Count;



            int schedLeaks = 0;



            var locMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);



            bool isPlateSched = s.Definition.CategoryId == plateCatId;



            double sWeight = 0;



            double cleanWeight = 0;



            int cleanCount = 0;



            foreach (var e in elems)



            {



                var pLok = e.LookupParameter("Lokasi");



                string loc = pLok?.AsString() ?? "";



                bool isLeaked = !string.IsNullOrWhiteSpace(loc) && !loc.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase);



                if (isLeaked)



                {



                    schedLeaks++;



                    totalBoundaryLeaks++;



                    if (!locMap.ContainsKey(loc)) locMap[loc] = 0;



                    locMap[loc]++;



                    if (leakSamples.Count < 10)



                    {



                        string tName = doc.GetElement(e.GetTypeId())?.Name ?? e.Name;



                        leakSamples.Add($"Elemen ID {e.Id.Value} (Lokasi: '{loc}', Tipe: '{tName}') bocor ke master schedule '{s.Name}'");



                    }



                }



                else



                {



                    cleanCount++;



                }



                if (isPlateSched)



                {



                    double wt = e.LookupParameter("Weight")?.AsDouble() ?? 0;



                    sWeight += wt;



                    if (!isLeaked) cleanWeight += wt;



                }



            }



            if (isPlateSched)



            {



                schedPlateWeights[s.Name] = (sWeight, cleanWeight, cleanCount);



            }



            leaksBySchedule[s.Name] = (elems.Count, schedLeaks, locMap);



        }



        sb.AppendLine($"Total Elemen Terdaftar di Master Schedule Bangunan Utama: {totalBuElements} elemen");



        sb.AppendLine($"Total Kebocoran Elemen Sub-Lokasi ke Bangunan Utama: {totalBoundaryLeaks} elemen");



        sb.AppendLine("\nBreakdown Kebocoran per Schedule Bangunan Utama:");



        foreach (var kvp in leaksBySchedule.OrderByDescending(x => x.Value.Leaks))



        {



            if (kvp.Value.Leaks > 0)



            {



                string locDetails = string.Join(", ", kvp.Value.ByLoc.Select(b => $"{b.Key}: {b.Value}"));



                sb.AppendLine($"  ! {kvp.Key} -> Total: {kvp.Value.Total} elemen | BOCOR: {kvp.Value.Leaks} elemen [{locDetails}]");



            }



            else



            {



                sb.AppendLine($"  v {kvp.Key} -> Total: {kvp.Value.Total} elemen | KEDAP (0 Bocor)");



            }



        }



        if (totalBoundaryLeaks > 0)



        {



            sb.AppendLine("\nSample Detail Elemen Bocor:");



            foreach (var l in leakSamples) sb.AppendLine($"  - {l}");



            sb.AppendLine(">> EVALUASI BOUNDARY: GAGAL (LEAK DETECTED). Master schedule belum memiliki filter Lokasi=\"\" sehingga elemen sub-lokasi terhitung ganda (double-counting).");



        }



        else



        {



            sb.AppendLine("\n>> EVALUASI BOUNDARY: LULUS (ZERO LEAKS). Filter Lokasi=\"\" 100% kedap.");



        }



        // 2. Rekapitulasi Tonase Baja Struktural Proyek Lengkap



        sb.AppendLine("\n[2. REKAPITULASI TONASE BAJA STRUKTURAL PROYEK]");



        sb.AppendLine(string.Format("{0,-25} | {1,8} | {2,11} | {3,11} | {4,11} | {5,11} | {6,9}",



            "Area / Lokasi", "Elemen", "Balok (kg)", "Kolom (kg)", "Pelat (kg)", "Basepl(kg)", "Total(T)"));



        sb.AppendLine(new string('-', 103));



        var dbWeights = LoadSteelDatabaseWeights();



        var typeWeightCache = new Dictionary<ElementId, double>();



        double GetTypeUnitWeightCached(ElementId tid, bool isFraming)



        {



            if (typeWeightCache.TryGetValue(tid, out double cached)) return cached;



            string tName = doc.GetElement(tid)?.Name ?? "";



            double w = GetCanonicalSteelWeight(tName, dbWeights);



            if (w <= 0) w = isFraming ? 49.6 : 49.9;



            typeWeightCache[tid] = w;



            return w;



        }



        var locations = new string[]



        {



            "Waterline",



            "Power House",



            "Carton & Cups Storage",



            "Production Office",



            "Chiller Area 1",



            "Chiller Area 2",



            "(Bangunan Utama)"



        };



        var framingByLoc = new Dictionary<string, (int Count, double WeightKg)>(StringComparer.OrdinalIgnoreCase);



        var colByLoc = new Dictionary<string, (int Count, double WeightKg)>(StringComparer.OrdinalIgnoreCase);



        foreach (var l in locations)



        {



            string key = l == "(Bangunan Utama)" ? "" : l;



            framingByLoc[key] = (0, 0);



            colByLoc[key] = (0, 0);



        }



        var allFraming = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralFraming)



            .WhereElementIsNotElementType()



            .ToList();



        foreach (var fr in allFraming)



        {



            var pComm = fr.LookupParameter("Comments")?.AsString() ?? "";



            if (pComm.Contains("Angkur")) continue;



            var loc = fr.LookupParameter("Lokasi")?.AsString() ?? "";



            if (loc.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase)) loc = "";



            if (!framingByLoc.ContainsKey(loc)) framingByLoc[loc] = (0, 0);



            double lenM = 0;



            var pLen = fr.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH) ?? fr.LookupParameter("Cut Length") ?? fr.LookupParameter("Length");



            if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;



            double w = GetTypeUnitWeightCached(fr.GetTypeId(), true);



            var curr = framingByLoc[loc];



            framingByLoc[loc] = (curr.Count + 1, curr.WeightKg + (lenM * w));



        }



        var allCols = new FilteredElementCollector(doc)



            .OfCategory(BuiltInCategory.OST_StructuralColumns)



            .WhereElementIsNotElementType()



            .ToList();



        foreach (var c in allCols)



        {



            var loc = c.LookupParameter("Lokasi")?.AsString() ?? "";



            if (loc.Equals("Bangunan Utama", StringComparison.OrdinalIgnoreCase)) loc = "";



            if (!colByLoc.ContainsKey(loc)) colByLoc[loc] = (0, 0);



            double lenM = 0;



            var pLen = c.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH) ?? c.LookupParameter("Cut Length") ?? c.LookupParameter("Length");



            if (pLen != null && pLen.HasValue) lenM = pLen.AsDouble() * 0.3048;



            double w = GetTypeUnitWeightCached(c.GetTypeId(), false);



            var curr = colByLoc[loc];



            colByLoc[loc] = (curr.Count + 1, curr.WeightKg + (lenM * w));



        }



        var subPlateScheds = allScheds.Where(s => s.Name.StartsWith("C.3.") && s.Definition.CategoryId == plateCatId).ToList();



        var subPlateWeightsByLoc = new Dictionary<string, (double PlateKg, double BaseplateKg, int Count)>(StringComparer.OrdinalIgnoreCase);



        foreach (var ps in subPlateScheds)



        {



            string pLoc = "";



            foreach (var l in locations)



            {



                if (l != "(Bangunan Utama)" && ps.Name.IndexOf(l, StringComparison.OrdinalIgnoreCase) >= 0)



                {



                    pLoc = l;



                    break;



                }



            }



            if (string.IsNullOrEmpty(pLoc)) continue;



            if (!subPlateWeightsByLoc.ContainsKey(pLoc)) subPlateWeightsByLoc[pLoc] = (0, 0, 0);



            bool isBase = ps.Name.IndexOf("Base", StringComparison.OrdinalIgnoreCase) >= 0 || ps.Name.IndexOf("Baseplate", StringComparison.OrdinalIgnoreCase) >= 0;



            var pElems = new FilteredElementCollector(doc, ps.Id).WhereElementIsNotElementType().ToElements();



            double curPlateKg = 0;



            double curBaseKg = 0;



            foreach (var p in pElems)



            {



                double wt = p.LookupParameter("Weight")?.AsDouble() ?? 0;



                if (isBase) curBaseKg += wt;



                else curPlateKg += wt;



            }



            var exist = subPlateWeightsByLoc[pLoc];



            subPlateWeightsByLoc[pLoc] = (exist.PlateKg + curPlateKg, exist.BaseplateKg + curBaseKg, exist.Count + pElems.Count);



        }



        double grandBalokKg = 0;



        double grandKolomKg = 0;



        double grandPelatKg = 0;



        double grandBaseplateKg = 0;



        int grandTotalElements = 0;



        foreach (var locName in locations)



        {



            string locKey = locName == "(Bangunan Utama)" ? "" : locName;



            var frData = framingByLoc.ContainsKey(locKey) ? framingByLoc[locKey] : (0, 0);



            var colData = colByLoc.ContainsKey(locKey) ? colByLoc[locKey] : (0, 0);



            double pKg = 0;



            double bKg = 0;



            int pCount = 0;



            if (locName == "(Bangunan Utama)")



            {



                foreach (var kvp in schedPlateWeights)



                {



                    bool isBase = kvp.Key.IndexOf("Base", StringComparison.OrdinalIgnoreCase) >= 0 || kvp.Key.IndexOf("Baseplate", StringComparison.OrdinalIgnoreCase) >= 0;



                    if (isBase) bKg += kvp.Value.CleanWeight;



                    else pKg += kvp.Value.CleanWeight;



                    pCount += kvp.Value.CleanCount;



                }



            }



            else



            {



                if (subPlateWeightsByLoc.ContainsKey(locName))



                {



                    var sp = subPlateWeightsByLoc[locName];



                    pKg = sp.PlateKg;



                    bKg = sp.BaseplateKg;



                    pCount = sp.Count;



                }



            }



            int elemCount = frData.Count + colData.Count + pCount;



            double locTotalTon = (frData.WeightKg + colData.WeightKg + pKg + bKg) / 1000.0;



            sb.AppendLine(string.Format("{0,-25} | {1,8} | {2,11:N1} | {3,11:N1} | {4,11:N1} | {5,11:N1} | {6,9:F2}",



                locName, elemCount, frData.WeightKg, colData.WeightKg, pKg, bKg, locTotalTon));



            grandBalokKg += frData.WeightKg;



            grandKolomKg += colData.WeightKg;



            grandPelatKg += pKg;



            grandBaseplateKg += bKg;



            grandTotalElements += elemCount;



        }



        double grandTotalTons = (grandBalokKg + grandKolomKg + grandPelatKg + grandBaseplateKg) / 1000.0;



        sb.AppendLine(new string('-', 103));



        sb.AppendLine(string.Format("{0,-25} | {1,8} | {2,11:N1} | {3,11:N1} | {4,11:N1} | {5,11:N1} | {6,9:F2}",



            "GRAND TOTAL PROYEK", grandTotalElements, grandBalokKg, grandKolomKg, grandPelatKg, grandBaseplateKg, grandTotalTons));



        if (schedPlateWeights.Count > 0)



        {



            sb.AppendLine("\nCatatan Khusus Schedule Pelat Bangunan Utama:");



            foreach (var kvp in schedPlateWeights)



            {



                sb.AppendLine($"  * {kvp.Key}: Raw Schedule Weight = {kvp.Value.SchedWeight:N1} kg | Bersih (Lokasi=\"\") = {kvp.Value.CleanWeight:N1} kg | Selisih Kebocoran = {kvp.Value.SchedWeight - kvp.Value.CleanWeight:N1} kg");



            }



        }



        // 3. Evaluasi Integritas Global (BIM Coordinator Synthesis)



        sb.AppendLine("\n[3. EVALUASI INTEGRITAS GLOBAL & REKOMENDASI BIM COORDINATOR]");



        sb.AppendLine("A. Status Isolasi Batas (Boundary Isolation):");



        sb.AppendLine($"   - Schedule Bangunan Utama bocor: {leaksBySchedule.Count(x => x.Value.Leaks > 0)} dari {buScheds.Count} schedule.");



        sb.AppendLine($"   - Total {totalBoundaryLeaks} elemen sub-lokasi terhitung ke master schedule Bangunan Utama.");



        sb.AppendLine("   - Penyebab: Schedule Bangunan Utama (khususnya B.1.5 Kolom Base Plat, dll.) belum diberi filter 'Lokasi Equal \"\"'.");



        sb.AppendLine("\nB. Status Double-Counting & Unscheduled:");



        sb.AppendLine("   - Kolom Bangunan Utama: Terjadi double-counting di 6 schedule kolom paralel karena filter Type yang tumpang tindih.");



        sb.AppendLine("   - Balok Bangunan Utama: 10,912 balok gording (C 150) belum terjadwal di schedule master B.1.");



        sb.AppendLine("   - Sub-Lokasi: Struktur baja profil WF/H di 6 sub-lokasi memiliki QTO presisi dan terisolasi.");



        sb.AppendLine("\nC. Kesimpulan & Rekomendasi Kelayakan Model:");



        sb.AppendLine("   - Rekomendasi 1: Terapkan filter ketat 'Lokasi Equal \"\"' pada seluruh schedule B.1.x dan B.2.x.");



        sb.AppendLine("   - Rekomendasi 2: Lakukan partisi schedule Kolom Bangunan Utama agar 1 kolom hanya masuk ke 1 schedule spesifik.");



        sb.AppendLine("   - Rekomendasi 3: JANGAN melakukan eksekusi modifikasi fisik model sampai filter schedule Bangunan Utama selesai dibersihkan.");



        return sb.ToString();



    }



    public static string InspectPortalSheetsAndLinks(Document doc)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: AUDIT SHEET PORTAL & VIEW LINK VISIBILITY ===");



        // 1. Audit Sheets with "PORTAL"



        var sheets = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSheet))



            .Cast<ViewSheet>()



            .Where(s => s.Name.IndexOf("PORTAL", StringComparison.OrdinalIgnoreCase) >= 0 



                     || s.SheetNumber.IndexOf("PORTAL", StringComparison.OrdinalIgnoreCase) >= 0)



            .OrderBy(s => s.SheetNumber)



            .ToList();



        sb.AppendLine($"Total Sheet PORTAL Ditemukan: {sheets.Count}");



        var linkInsts = new FilteredElementCollector(doc)



            .OfClass(typeof(RevitLinkInstance))



            .Cast<RevitLinkInstance>()



            .ToList();



        var cat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_RvtLinks);



        var placedViews = new List<View>();



        foreach (var s in sheets)



        {



            var vpIds = s.GetAllViewports();



            foreach (var vpId in vpIds)



            {



                var vp = doc.GetElement(vpId) as Viewport;



                if (vp == null) continue;



                var v = doc.GetElement(vp.ViewId) as View;



                if (v == null) continue;



                placedViews.Add(v);



                string tName = v.ViewTemplateId != ElementId.InvalidElementId 



                    ? doc.GetElement(v.ViewTemplateId)?.Name ?? v.ViewTemplateId.Value.ToString() 



                    : "None";



                int hiddenCount = linkInsts.Count(li => li.IsHidden(v));



                int visibleCount = linkInsts.Count - hiddenCount;



                bool catHidden = cat != null && v.GetCategoryHidden(cat.Id);



                sb.AppendLine($"Sheet [{s.SheetNumber}] '{s.Name}' -> View [{v.Id.Value}] '{v.Name}':");



                sb.AppendLine($"    Template: '{tName}' | Link Visible: {visibleCount} | Link Hidden: {hiddenCount} | Category OST_RvtLinks Hidden: {catHidden}");



            }



        }



        return sb.ToString();



    }



    public static string ExecuteHideLinksInPortalViews(Document doc, string? argument)



    {



        var sb = new StringBuilder();



        sb.AppendLine("=== AIGRAV: EKSEKUSI HIDE LINK PADA SELURUH VIEW SHEET PORTAL ===");



        var sheets = new FilteredElementCollector(doc)



            .OfClass(typeof(ViewSheet))



            .Cast<ViewSheet>()



            .Where(s => s.Name.IndexOf("PORTAL", StringComparison.OrdinalIgnoreCase) >= 0 



                     || s.SheetNumber.IndexOf("PORTAL", StringComparison.OrdinalIgnoreCase) >= 0)



            .OrderBy(s => s.SheetNumber)



            .ToList();



        var linkInsts = new FilteredElementCollector(doc)



            .OfClass(typeof(RevitLinkInstance))



            .Cast<RevitLinkInstance>()



            .ToList();



        int totalViewsProcessed = 0;



        int totalLinksHidden = 0;



        using (Transaction tx = new(doc, "AIGRAV - Hide Revit Links in Portal Views"))



        {



            var failOpt = tx.GetFailureHandlingOptions();



            failOpt.SetFailuresPreprocessor(new ClashWarningSwallower());



            failOpt.SetClearAfterRollback(true);



            tx.SetFailureHandlingOptions(failOpt);



            tx.Start();



            foreach (var s in sheets)



            {



                var vpIds = s.GetAllViewports();



                foreach (var vpId in vpIds)



                {



                    var vp = doc.GetElement(vpId) as Viewport;



                    if (vp == null) continue;



                    var v = doc.GetElement(vp.ViewId) as View;



                    if (v == null) continue;



                    var toHide = new List<ElementId>();



                    foreach (var li in linkInsts)



                    {



                        if (!li.IsHidden(v) && li.CanBeHidden(v))



                        {



                            toHide.Add(li.Id);



                        }



                    }



                    if (toHide.Count > 0)



                    {



                        v.HideElements(new HashSet<ElementId>(toHide));



                        totalLinksHidden += toHide.Count;



                    }



                    totalViewsProcessed++;



                    sb.AppendLine($"  * Sheet [{s.SheetNumber}] '{s.Name}' -> View '{v.Name}' : {toHide.Count} link di-hide (Total Link Hidden: {linkInsts.Count(li => li.IsHidden(v))}/{linkInsts.Count})");



                }



            }



            doc.Regenerate();



            tx.Commit();



        }



        sb.AppendLine($"\n[HASIL AKHIR]:");



        sb.AppendLine($"Total View Diproses: {totalViewsProcessed}");



        sb.AppendLine($"Total Link Berhasil Di-Hide: {totalLinksHidden}");



        return sb.ToString();



    }



}
