SWIFT MT102 With Message Example
SWIFT MT102 is a type of financial message format used in the SWIFT (Society for Worldwide Interbank Financial Telecommunication) network. It’s a multi-customer credit transfer message used for domestic and international payments. Here’s a breakdown of the key components and aspects of the MT102 message:

Appearance and Format: SWIFT messages are formatted in plain text and follow a specific structure. The MT102 message, like other SWIFT messages, consists of a series of fields in a standardized order. Each field starts with a field tag (a number) enclosed in curly brackets.
Fields Involved: The MT102 includes various fields that convey information about the transaction. These fields include details like the sender and receiver’s bank information, transaction amounts, dates, and customer details.
Mandatory vs Optional Fields: The message is divided into mandatory and optional fields. Mandatory fields must be included for the message to be valid, while optional fields are used as needed to provide additional information. For example, fields like the transaction amount, date, and details of the involved parties are typically mandatory, while certain reference or narrative fields might be optional.
Submitting the Message: The message is submitted through the SWIFT network, which is a secure and standardized platform used by financial institutions globally. Banks and other financial entities use SWIFT to send and receive information about financial transactions in a reliable and secure manner.
How the MT102 Message Works: The MT102 is used for batched payment instructions, meaning it can include multiple credit transfer instructions in one message. This is particularly useful for institutions making numerous transactions at once. Each transaction within the MT102 is treated individually, but batching them together in one message streamlines the process and reduces costs.
To learn more about the specific format, including the exact fields and their order, you would typically refer to the SWIFT User Handbook, which is a detailed guide provided by SWIFT to its members. The handbook contains comprehensive information about each message type, including the MT102. Additionally, many banks and financial software providers offer training or guides on understanding and using SWIFT messages, which can be a valuable resource for in-depth learning.

Here is an example of what a SWIFT MT102 message might look like. Please note that in a real scenario, the message would contain specific financial data and would adhere to strict formatting rules set by SWIFT. The example below is for illustrative purposes only to give you an idea of the structure and types of information included:

{1:F01YOURBANKXXXXX0000000000}{2:I102RECEIVBANKXXXXN}{4:
:20:REFERENCE12345
:21:RELATEDREF
:32B:EUR1000,00
:50A:/123456789
FOO COMPANY
:52A:FOOBANK
:57A:RECEIVBANK
:59:/234567890
BAR COMPANY
STREET ADDRESS
CITY, COUNTRY
:70:INVOICE 789
:71A:SHA
-}
{1:F01YOURBANKXXXXX0000000000}{2:I102RECEIVBANKXXXXN}{4:
:20:REFERENCE12346
:21:RELATEDREF
:32B:USD2000,00
:50K:/987654321
XYZ CORPORATION
:52A:XYZBANK
:57A:RECEIVBANK
:59:/345678901
ABC COMPANY
STREET ADDRESS
CITY, COUNTRY
:70:INVOICE 456
:71A:SHA
-}
Explanation of Fields:

{1:...} and {2:...} are header blocks containing the sender and receiver’s SWIFT codes.
:20: Transaction Reference Number.
:21: Related Reference.
:32B: Currency and Amount.
:50A: or :50K: Ordering Customer.
:52A: Ordering Institution.
:57A: Account With Institution.
:59: Beneficiary Customer.
:70: Remittance Information.
:71A: Details of Charges.
Each transaction within the MT102 is separated by a dash (-) and has its own set of fields. This example includes two transactions (each set of fields between {4: and -} is one transaction).

Remember, this is a highly simplified example. Real SWIFT messages contain sensitive financial information and must be handled with strict security and privacy measures. They also follow precise formatting rules that are essential for the correct processing of the messages by banks and financial institutions.

Here’s a more detailed example of a SWIFT MT102 message, which is complex and includes various fields commonly used in these types of messages. This is a fictional example for illustration purposes, as actual SWIFT messages contain sensitive and confidential financial information.

{1:F01YOURBANKXXXXX2123456789}{2:I102RECEIVERBKXXXXN}{3:{108:MT102EXAMPLE}}{4:
:20:TXNREF112233
:23:E
:50H:/123456789
NAME OF SENDER
ADDRESS LINE 1
ADDRESS LINE 2
COUNTRY
:30:210520
:52A:SENDERBK
:57A:/B/890123456
RECEIVERBK
:59:/987654321
NAME OF RECIPIENT
ADDRESS LINE 1
ADDRESS LINE 2
COUNTRY
:70:INVOICE PAYMENT 12345
:32B:USD12345,67
:71A:OUR
-}
{1:F01YOURBANKXXXXX2123456789}{2:I102RECEIVERBKXXXXN}{3:{108:MT102EXAMPLE}}{4:
:20:TXNREF112234
:23:E
:50H:/987654321
ANOTHER SENDER
ADDRESS LINE 1
ADDRESS LINE 2
COUNTRY
:30:210521
:52A:ANOTHERBK
:57A:/B/890123457
RECEIVERBK
:59:/123456789
ANOTHER RECIPIENT
ADDRESS LINE 1
ADDRESS LINE 2
COUNTRY
:70:INVOICE PAYMENT 67890
:32B:EUR7654,32
:71A:OUR
-}
Explanation of Fields:

{1:F01YOURBANKXXXXX2123456789}: This is the Basic Header Block, indicating the message type (F01), the sender’s bank code (YOURBANKXXXXX), and a session number.
{2:I102RECEIVERBKXXXXN}: The Application Header Block, showing the message type (I102) and the receiver’s bank code (RECEIVERBKXXXXN).
{3:{108:MT102EXAMPLE}}: The User Header Block, which can include optional reference numbers or identifiers; here it’s “MT102EXAMPLE”.
:20:TXNREF112233: The transaction reference number for the first transaction.
:23:E: Bank Operation Code (optional), here ‘E’ might represent a specific type of transaction or service.
:50H:: The ordering customer with account number, name, and address.
:30:210520: The date the transaction is instructed, in YYMMDD format.
:52A:SENDERBK: The ordering institution’s SWIFT code.
:57A:/B/890123456 RECEIVERBK: The account with institution’s information, including a sort code or account number followed by the institution’s SWIFT code.
:59:/987654321 NAME OF RECIPIENT: The beneficiary customer with their account number and name.
:70:INVOICE PAYMENT 12345: Remittance information, such as a description of the payment purpose.
:32B:USD12345,67: The currency and amount of the transaction.
:71A:OUR: Details of charges, ‘OUR’ indicates that the sender pays all transaction charges.
The second transaction (after the -}) follows a similar format, with its own unique details. Each transaction within the MT102 message is treated individually, allowing for batch processing of multiple transactions in a single message.

Again, keep in mind that this example is highly simplified and doesn’t include all possible fields or the complexity of real-world financial transactions. The actual usage of these messages requires thorough understanding of SWIFT standards and financial regulations.
