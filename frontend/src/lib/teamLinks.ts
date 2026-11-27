export const teamViewLink = (teamId: string, token: string) => `${location.origin}/#/tv/${teamId}?t=${token}`

export const teamJoinLink = (code: string) => `${location.origin}/#/team/join/${code}`
