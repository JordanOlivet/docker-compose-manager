/**
 * Utility functions for working with Compose projects
 */

import { ProjectStatus, PermissionFlags, type ComposeProjectDto, EntityState } from '$lib/types';

/**
 * Check if user has a specific permission on a project
 * @param project - The compose project
 * @param permission - The permission to check
 * @returns true if the user has the permission
 */
export function hasPermission(project: ComposeProjectDto, permission: PermissionFlags): boolean {
	return (project.userPermissions & permission) === permission;
}

/**
 * Check if user can start the project
 * @param project - The compose project
 * @returns true if the user can start the project
 */
export function canStartProject(project: ComposeProjectDto): boolean {
	const hasStartPermission = hasPermission(project, PermissionFlags.Start);
	const canStart = project.state === EntityState.Stopped || project.state === EntityState.Exited;
	return hasStartPermission && canStart;
}

/**
 * Check if user can stop the project
 * @param project - The compose project
 * @returns true if the user can stop the project
 */
export function canStopProject(project: ComposeProjectDto): boolean {
	const hasStopPermission = hasPermission(project, PermissionFlags.Stop);
	const canStop = project.state === EntityState.Running;
	return hasStopPermission && canStop;
}

/**
 * Check if user can restart the project
 * @param project - The compose project
 * @returns true if the user can restart the project
 */
export function canRestartProject(project: ComposeProjectDto): boolean {
	const hasRestartPermission = hasPermission(project, PermissionFlags.Restart);
	const canRestart = project.state === EntityState.Running;
	return hasRestartPermission && canRestart;
}

/**
 * Get the color for a project status (Tailwind CSS classes)
 * @param status - The project status
 * @returns Tailwind color class name
 */
export function getStatusColor(status: ProjectStatus): string {
	switch (status) {
		case ProjectStatus.Running:
			return 'green';
		case ProjectStatus.Stopped:
			return 'orange';
		case ProjectStatus.Removed:
			return 'gray';
		case ProjectStatus.Unknown:
		default:
			return 'red';
	}
}

/**
 * Get status badge classes for Tailwind CSS
 * @param status - The project status
 * @returns Object with bg, text, and border classes
 */
export function getStatusBadgeClasses(state: EntityState): {
	bg: string;
	text: string;
	border: string;
} {
	switch (state) {
		case EntityState.Running:
			return {
				bg: 'bg-green-100',
				text: 'text-green-800',
				border: 'border-green-200'
			};
		case EntityState.Stopped:
			return {
				bg: 'bg-orange-100',
				text: 'text-orange-800',
				border: 'border-orange-200'
			};
		case EntityState.Exited:
			return {
				bg: 'bg-gray-100',
				text: 'text-gray-800',
				border: 'border-gray-200'
			};
		case EntityState.Unknown:
		default:
			return {
				bg: 'bg-red-100',
				text: 'text-red-800',
				border: 'border-red-200'
			};
	}
}

// /**
//  * Format the project status for display
//  * @param project - The compose project
//  * @returns Formatted status string
//  */
// export function formatProjectStatus(project: ComposeProjectDto): string {
// 	const statusLabel = project.state;
// 	const containerInfo =
// 		project.containerCount > 0 ? ` (${project.containerCount} container${project.containerCount > 1 ? 's' : ''})` : '';
// 	return `${statusLabel}${containerInfo}`;
// }

/**
 * Get a human-readable permission name
 * @param permission - The permission flag
 * @returns Human-readable name
 */
export function getPermissionName(permission: PermissionFlags): string {
	switch (permission) {
		case PermissionFlags.View:
			return 'View';
		case PermissionFlags.Start:
			return 'Start';
		case PermissionFlags.Stop:
			return 'Stop';
		case PermissionFlags.Restart:
			return 'Restart';
		case PermissionFlags.Delete:
			return 'Delete';
		case PermissionFlags.Update:
			return 'Update';
		case PermissionFlags.Logs:
			return 'Logs';
		case PermissionFlags.Execute:
			return 'Execute';
		case PermissionFlags.Full:
			return 'Full Control';
		default:
			return 'None';
	}
}

/**
 * Get all permissions a user has on a project
 * @param project - The compose project
 * @returns Array of permission names
 */
export function getUserPermissions(project: ComposeProjectDto): string[] {
	const permissions: string[] = [];
	const flags = [
		PermissionFlags.View,
		PermissionFlags.Start,
		PermissionFlags.Stop,
		PermissionFlags.Restart,
		PermissionFlags.Delete,
		PermissionFlags.Update,
		PermissionFlags.Logs,
		PermissionFlags.Execute
	];

	for (const flag of flags) {
		if (hasPermission(project, flag)) {
			permissions.push(getPermissionName(flag));
		}
	}

	return permissions;
}
