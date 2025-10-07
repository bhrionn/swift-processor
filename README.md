
OVERVIEW
We require to write a processor that receives MT103  SWIFT messages, processes them and adds the processed messages to a queue for further processing or analysis (dead letter queue).

The processor should have two parts 
1.) Typescript web front end to see the received messages.
    Typescript web site should use modern web app best practices. THe UI will simply read the local database to see what is processed, what is pending, what is failed.
    The website can restart the processor. 

2.) Dotnet Core 9.0 backend processor that reads SWIFT MT103  messages from a queue (local initially , but will be AWS queue), processes the MT103, updates the local database with processed into and moves the messages to completed queue.
    THe backend processor should have all local queue functionality initially for testing and debug. THis will be replaced with AWS queue when in Staging.
    Database should be a local database that will be switched to production SQL Server once testing is competed 
    The backend should have a test mode that periodically generates various MT103 swif messages and adds them to the queue, ie. data sumulator for real timve testing of system.
    The back end processor should be designed using SOLID design principles, dotnet code and extendable. We intend to process additional messages once this prototype is completed. i.e. MT102 etc.


Swift Message Info:
SWIFT messages are standardized financial messages sent securely between banks, identified by a three-digit message type code (MT) and a Bank Identifier Code (BIC). The MT code indicates the message's purpose, such as MT 103 for a single customer credit transfer, while the BIC identifies the specific bank, consisting of an 8 or 11-character code with the bank code, country code, location code, and optionally a branch code.i


SWIFT Message (MT) Codes
Structure: A three-digit code starting with "MT," for example, MT 103. 
Purpose: The first digit designates a category (e.g., '1' for customer payments), the second digit identifies a group within that category (e.g., '0' for financial institution transfers), and the third digit specifies the exact message type. 
Examples:
MT 103: A single customer credit transfer. 
MT 760: Used for bank guarantees and standby letters of credit. 
MT 90: Advises charges, interest, or other adjustments on an account. 
Bank Identifier Code (BIC)
Also Known As: A SWIFT code, these terms are often used interchangeably. 
Structure: An 8 or 11-character code. 
Components:
Bank Code: A shortened version of the bank's name. 
Country Code: Identifies the bank's country of origin. 
Location Code: Specifies the area within the country. 
Branch Code (Optional): Identifies a specific branch of the bank. 
Key Information for SWIFT Payments
To make a SWIFT payment, you need several pieces of information: 
The name and address of the recipient (payee).
The name and address of the recipient's bank.
The recipient's account number.
The SWIFT code (BIC) of the recipient's bank.
