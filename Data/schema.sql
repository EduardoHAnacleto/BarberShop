CREATE TABLE Services (
    ServiceId INT AUTO_INCREMENT PRIMARY KEY,
    ServiceName VARCHAR(30) NOT NULL,
	ServicePrice decimal(10,2) NOT NULL,
	ServiceDescription VARCHAR(30),
	ServiceDuration int NOT NULL,
);

CREATE TABLE Costumers (
    CostumerId INT AUTO_INCREMENT PRIMARY KEY,
    CostumerName VARCHAR(50) NOT NULL,
    CostumerDateOfBirth datetime,
    CostumerEmail varchar(25),
    CostumerPhoneNumber VARCHAR(14),
);

CREATE TABLE Workers (
    WorkerId INT AUTO_INCREMENT PRIMARY KEY,
    WorkerName VARCHAR(50) NOT NULL,
    WorkerDateOfBirth datetime NOT NULL,
    WorkerAddress varchar(50) NOT NULL,
	WorkerPosition varchar(25) NOT NULL,
	WorkerPhoneNumber varchar(14) NOT NULL,
	WorkerWagePerHour decimal(10,2) NOT NULL,
);

CREATE TABLE WorkerServices (
    WSWorkerId INT NOT NULL,
    WSServiceId INT NOT NULL,	
	    CONSTRAINT fk_service_worker
        FOREIGN KEY (WSServiceId) REFERENCES categories(ServiceId)
		CONSTRAINT fk_worker_service
        FOREIGN KEY (WSWorkerId) REFERENCES categories(WorkerId)
		CONSTRAINT PK_worker_service
        PRIMARY KEY (WSWorkerId, WSServiceId)
);

CREATE TABLE Appointments (
    AppointmentId INT AUTO_INCREMENT PRIMARY KEY,
    AppointmentServiceId int NOT NULL,
    AppointmentStatus varchar(15) NOT NULL,
    AppointmentScheduledFor datetime NOT NULL,
	AppointmentExtraDetails varchar(100) NOT NULL,
	AppointmentCompletedAt datetime,
	AppointmentCustomerId int NOT NULL,
	AppointmentWorkerId int NOT NULL,
	    CONSTRAINT fk_appointmentServiceId
        FOREIGN KEY (AppointmentServiceId) REFERENCES categories(ServiceId)
	    CONSTRAINT fk_appointmentCustomerId
        FOREIGN KEY (AppointmentCustomerId) REFERENCES categories(CostumerId)
	    CONSTRAINT fk_appointmentWorkerId
        FOREIGN KEY (AppointmentWorkerId) REFERENCES categories(WorkerId)	
);

CREATE TABLE Users (
    UserId INT AUTO_INCREMENT PRIMARY KEY,
    PasswordHash NVARCHAR(255) NOT NULL,
    UsesrRole NVARCHAR(50) NOT NULL DEFAULT 'User',
    UserIsActive BIT NOT NULL DEFAULT 1,
    UserWorkerId INT NULL,
    UserCustomerId Int NULL,
    UserCreatedAt DATETIME2 NOT NULL,
);
