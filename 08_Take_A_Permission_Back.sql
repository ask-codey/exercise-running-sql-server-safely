-- 08. Last month someone gave the shift lead SELECT on Employees WITH GRANT OPTION, and the shift lead passed it on to a new hire.
--     Neither of them should read Employees. The lines below set that situation up again; leave them as they are.
--     Under them, write one statement that takes SELECT on Employees back from ShiftLead and from everyone ShiftLead gave it to.
-- Expect: Neither ShiftLead nor NewHire has SELECT on Employees.

DROP USER IF EXISTS NewHire;
DROP USER IF EXISTS ShiftLead;
CREATE USER ShiftLead WITHOUT LOGIN;
CREATE USER NewHire WITHOUT LOGIN;
GRANT SELECT ON Employees TO ShiftLead WITH GRANT OPTION;
EXECUTE AS USER = 'ShiftLead';
GRANT SELECT ON Employees TO NewHire;
REVERT;

-- Write your statement below.
